using AVFoundation;
using CoreAnimation;
using CoreFoundation;
using CoreGraphics;
using Foundation;
using MeterRecti.Native.Services;
using UIKit;

namespace MeterRecti.Native.Platforms.iOS;

public sealed class IosScannerService : IScannerService
{
	public async Task<string?> ScanAsync(CancellationToken cancellationToken)
	{
		var status = AVCaptureDevice.GetAuthorizationStatus(AVAuthorizationMediaType.Video);
		if (status == AVAuthorizationStatus.NotDetermined)
		{
			var granted = await AVCaptureDevice.RequestAccessForMediaTypeAsync(AVAuthorizationMediaType.Video);
			if (!granted)
			{
				throw new InvalidOperationException("相机权限未开启，无法扫码。");
			}
		}
		else if (status != AVAuthorizationStatus.Authorized)
		{
			throw new InvalidOperationException("相机权限未开启，请在系统设置中允许 Meter Recti 使用相机。");
		}

		var presenter = GetTopViewController() ?? throw new InvalidOperationException("无法打开扫码界面。");
		var completion = new TaskCompletionSource<string?>();
		var controller = new ScannerViewController(completion);
		controller.ModalPresentationStyle = UIModalPresentationStyle.FullScreen;

		await MainThread.InvokeOnMainThreadAsync(() => presenter.PresentViewController(controller, true, null));

		await using (cancellationToken.Register(() =>
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				controller.DismissViewController(true, null);
				completion.TrySetCanceled(cancellationToken);
			});
		}))
		{
			return await completion.Task;
		}
	}

	private static UIViewController? GetTopViewController()
	{
		var window = UIApplication.SharedApplication.ConnectedScenes
			.OfType<UIWindowScene>()
			.SelectMany(scene => scene.Windows)
			.FirstOrDefault(candidate => candidate.IsKeyWindow);

		var controller = window?.RootViewController;
		while (controller?.PresentedViewController is not null)
		{
			controller = controller.PresentedViewController;
		}

		return controller;
	}
}

internal sealed class ScannerViewController : UIViewController, IAVCaptureMetadataOutputObjectsDelegate
{
	private readonly TaskCompletionSource<string?> completion;
	private readonly AVCaptureSession session = new();
	private readonly DispatchQueue sessionQueue = new("meter-recti.scanner.session");
	private readonly AVCaptureMetadataOutput metadataOutput = new();
	private readonly UIView scanFrameView = new();
	private readonly UILabel instructionsLabel = new();
	private readonly UIStackView zoomStack = new();
	private readonly UIButton torchButton = UIButton.FromType(UIButtonType.System);
	private AVCaptureVideoPreviewLayer? previewLayer;
	private AVCaptureDevice? captureDevice;
	private bool hasFinished;

	public ScannerViewController(TaskCompletionSource<string?> completion)
	{
		this.completion = completion;
	}

	public override void ViewDidLoad()
	{
		base.ViewDidLoad();
		View!.BackgroundColor = UIColor.Black;
		SetupPreview();
		SetupOverlay();
		View.AddGestureRecognizer(new UITapGestureRecognizer(FocusTapped) { CancelsTouchesInView = false });
		ConfigureSession();
	}

	public override void ViewDidLayoutSubviews()
	{
		base.ViewDidLayoutSubviews();
		previewLayer!.Frame = View!.Bounds;
		LayoutScanFrame();
		UpdateRectOfInterest();
	}

	public override void ViewWillDisappear(bool animated)
	{
		base.ViewWillDisappear(animated);
		SetTorch(false);
		sessionQueue.DispatchAsync(() =>
		{
			if (session.Running)
			{
				session.StopRunning();
			}
		});
	}

	private void SetupPreview()
	{
		previewLayer = new AVCaptureVideoPreviewLayer(session)
		{
			VideoGravity = AVLayerVideoGravity.ResizeAspectFill
		};
		View!.Layer.AddSublayer(previewLayer);
	}

	private void SetupOverlay()
	{
		var dimView = new UIView
		{
			TranslatesAutoresizingMaskIntoConstraints = false,
			BackgroundColor = UIColor.Black.ColorWithAlpha(0.22f)
		};
		View!.AddSubview(dimView);
		NSLayoutConstraint.ActivateConstraints(
		[
			dimView.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
			dimView.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
			dimView.TopAnchor.ConstraintEqualTo(View.TopAnchor),
			dimView.BottomAnchor.ConstraintEqualTo(View.BottomAnchor)
		]);

		scanFrameView.Layer.BorderColor = UIColor.FromRGB(155, 200, 251).CGColor;
		scanFrameView.Layer.BorderWidth = 3;
		scanFrameView.Layer.CornerRadius = 14;
		scanFrameView.BackgroundColor = UIColor.Clear;
		View.AddSubview(scanFrameView);

		instructionsLabel.TranslatesAutoresizingMaskIntoConstraints = false;
		instructionsLabel.Text = "请将二维码或条形码置于取景框内";
		instructionsLabel.TextAlignment = UITextAlignment.Center;
		instructionsLabel.TextColor = UIColor.White;
		instructionsLabel.Font = UIFont.SystemFontOfSize(17, UIFontWeight.Semibold);
		instructionsLabel.Lines = 2;
		View.AddSubview(instructionsLabel);

		var cancelButton = MakeOverlayButton("取消");
		cancelButton.TouchUpInside += (_, _) => Cancel();
		View.AddSubview(cancelButton);

		torchButton.TranslatesAutoresizingMaskIntoConstraints = false;
		StyleOverlayButton(torchButton, "手电筒");
		torchButton.TouchUpInside += (_, _) => ToggleTorch();
		torchButton.Hidden = true;
		View.AddSubview(torchButton);

		zoomStack.TranslatesAutoresizingMaskIntoConstraints = false;
		zoomStack.Axis = UILayoutConstraintAxis.Horizontal;
		zoomStack.Alignment = UIStackViewAlignment.Center;
		zoomStack.Distribution = UIStackViewDistribution.FillEqually;
		zoomStack.Spacing = 8;
		foreach (var zoom in new[] { 1.0f, 1.5f, 2.0f })
		{
			var button = MakeOverlayButton($"{zoom:0.#}x");
			button.Tag = (nint)(zoom * 10);
			button.TouchUpInside += (_, _) => ApplyZoom(button.Tag / 10f);
			zoomStack.AddArrangedSubview(button);
		}
		View.AddSubview(zoomStack);

		NSLayoutConstraint.ActivateConstraints(
		[
			cancelButton.LeadingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.LeadingAnchor, 18),
			cancelButton.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor, 12),
			torchButton.TrailingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TrailingAnchor, -18),
			torchButton.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor, 12),
			instructionsLabel.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor, 24),
			instructionsLabel.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor, -24),
			instructionsLabel.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor, 82),
			zoomStack.CenterXAnchor.ConstraintEqualTo(View.CenterXAnchor),
			zoomStack.BottomAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.BottomAnchor, -28),
			zoomStack.WidthAnchor.ConstraintEqualTo(230),
			zoomStack.HeightAnchor.ConstraintEqualTo(42)
		]);
	}

	private static UIButton MakeOverlayButton(string title)
	{
		var button = UIButton.FromType(UIButtonType.System);
		button.TranslatesAutoresizingMaskIntoConstraints = false;
		StyleOverlayButton(button, title);
		return button;
	}

	private static void StyleOverlayButton(UIButton button, string title)
	{
		button.SetTitle(title, UIControlState.Normal);
		button.SetTitleColor(UIColor.White, UIControlState.Normal);
		button.TitleLabel!.Font = UIFont.SystemFontOfSize(17, UIFontWeight.Semibold);
		button.BackgroundColor = UIColor.Black.ColorWithAlpha(0.42f);
		button.Layer.CornerRadius = 18;
	}

	private void LayoutScanFrame()
	{
		var bounds = View!.Bounds;
		var side = (nfloat)Math.Min(Math.Min(bounds.Width * 0.82, bounds.Height * 0.48), 390);
		var height = side * 0.72;
		scanFrameView.Frame = new CGRect((bounds.Width - side) / 2, (bounds.Height - height) / 2, side, height);
	}

	private void ConfigureSession()
	{
		sessionQueue.DispatchAsync(() =>
		{
			session.BeginConfiguration();
			session.SessionPreset = AVCaptureSession.PresetHigh;

			var device = AVCaptureDevice.GetDefaultDevice(AVCaptureDeviceType.BuiltInWideAngleCamera, AVMediaTypes.Video, AVCaptureDevicePosition.Back)
				?? AVCaptureDevice.GetDefaultDevice(AVMediaTypes.Video);
			if (device is null)
			{
				session.CommitConfiguration();
				Fail("没有可用的相机。");
				return;
			}

			captureDevice = device;
			MainThread.BeginInvokeOnMainThread(() => torchButton.Hidden = !device.HasTorch);

			try
			{
				var input = AVCaptureDeviceInput.FromDevice(device);
				if (input is null)
				{
					session.CommitConfiguration();
					Fail("无法访问相机输入。");
					return;
				}

				if (session.CanAddInput(input))
				{
					session.AddInput(input);
				}

				if (session.CanAddOutput(metadataOutput))
				{
					session.AddOutput(metadataOutput);
					metadataOutput.SetDelegate(this, DispatchQueue.MainQueue);
					var requestedTypes = MetadataTypes();
					var availableTypes = metadataOutput.AvailableMetadataObjectTypes;
					var enabledTypes = requestedTypes
						.Where(type => availableTypes.HasFlag(type))
						.Aggregate((AVMetadataObjectType)0, (current, type) => current | type);
					metadataOutput.MetadataObjectTypes = enabledTypes == 0 ? availableTypes : enabledTypes;
				}

				ConfigureCamera(device);
				session.CommitConfiguration();
				session.StartRunning();
			}
			catch (Exception ex)
			{
				session.CommitConfiguration();
				Fail($"无法启动相机：{ex.Message}");
			}
		});
	}

	private void ConfigureCamera(AVCaptureDevice device)
	{
		NSError? error;
		if (!device.LockForConfiguration(out error))
		{
			return;
		}

		try
		{
			if (device.IsFocusModeSupported(AVCaptureFocusMode.ContinuousAutoFocus))
			{
				device.FocusMode = AVCaptureFocusMode.ContinuousAutoFocus;
			}

			if (device.SmoothAutoFocusSupported)
			{
				device.SmoothAutoFocusEnabled = true;
			}

			if (device.IsExposureModeSupported(AVCaptureExposureMode.ContinuousAutoExposure))
			{
				device.ExposureMode = AVCaptureExposureMode.ContinuousAutoExposure;
			}

			if (device.IsWhiteBalanceModeSupported(AVCaptureWhiteBalanceMode.ContinuousAutoWhiteBalance))
			{
				device.WhiteBalanceMode = AVCaptureWhiteBalanceMode.ContinuousAutoWhiteBalance;
			}

			device.VideoZoomFactor = (nfloat)Math.Min(device.ActiveFormat.VideoMaxZoomFactor, 1.5f);
		}
		finally
		{
			device.UnlockForConfiguration();
		}
	}

	private void ApplyZoom(nfloat zoom)
	{
		var device = captureDevice;
		if (device is null)
		{
			return;
		}

		NSError? error;
		if (!device.LockForConfiguration(out error))
		{
			return;
		}

		try
		{
			var maxZoom = (nfloat)Math.Min(device.ActiveFormat.VideoMaxZoomFactor, 4.0f);
			device.VideoZoomFactor = (nfloat)Math.Min(Math.Max(zoom, 1.0f), maxZoom);
		}
		finally
		{
			device.UnlockForConfiguration();
		}
	}

	private void UpdateRectOfInterest()
	{
		if (previewLayer is null || !metadataOutput.RespondsToSelector(new ObjCRuntime.Selector("setRectOfInterest:")))
		{
			return;
		}

		metadataOutput.RectOfInterest = previewLayer.MapToMetadataOutputCoordinates(scanFrameView.Frame);
	}

	private static AVMetadataObjectType[] MetadataTypes()
	{
		return
		[
			AVMetadataObjectType.QRCode,
			AVMetadataObjectType.Code128Code,
			AVMetadataObjectType.Code39Code,
			AVMetadataObjectType.Code93Code,
			AVMetadataObjectType.EAN13Code,
			AVMetadataObjectType.EAN8Code,
			AVMetadataObjectType.Interleaved2of5Code,
			AVMetadataObjectType.UPCECode,
			AVMetadataObjectType.PDF417Code,
			AVMetadataObjectType.AztecCode,
			AVMetadataObjectType.DataMatrixCode
		];
	}

	[Export("captureOutput:didOutputMetadataObjects:fromConnection:")]
	public void DidOutputMetadataObjects(AVCaptureMetadataOutput output, AVMetadataObject[] metadataObjects, AVCaptureConnection connection)
	{
		if (hasFinished)
		{
			return;
		}

		var readable = metadataObjects.OfType<AVMetadataMachineReadableCodeObject>().FirstOrDefault();
		var value = readable?.StringValue;
		if (string.IsNullOrWhiteSpace(value))
		{
			return;
		}

		hasFinished = true;
		new UINotificationFeedbackGenerator().NotificationOccurred(UINotificationFeedbackType.Success);
		SetTorch(false);
		sessionQueue.DispatchAsync(session.StopRunning);
		DismissViewController(true, () => completion.TrySetResult(value));
	}

	private void Cancel()
	{
		if (hasFinished)
		{
			return;
		}

		hasFinished = true;
		SetTorch(false);
		DismissViewController(true, () => completion.TrySetResult(null));
	}

	private void ToggleTorch()
	{
		var device = captureDevice;
		if (device?.HasTorch != true)
		{
			return;
		}

		SetTorch(device.TorchMode != AVCaptureTorchMode.On);
	}

	private void FocusTapped(UITapGestureRecognizer gesture)
	{
		var device = captureDevice;
		if (previewLayer is null || device is null)
		{
			return;
		}

		var point = gesture.LocationInView(View);
		var devicePoint = previewLayer.CaptureDevicePointOfInterestForPoint(point);

		NSError? error;
		if (!device.LockForConfiguration(out error))
		{
			return;
		}

		try
		{
			if (device.FocusPointOfInterestSupported)
			{
				device.FocusPointOfInterest = devicePoint;
				if (device.IsFocusModeSupported(AVCaptureFocusMode.AutoFocus))
				{
					device.FocusMode = AVCaptureFocusMode.AutoFocus;
				}
			}

			if (device.ExposurePointOfInterestSupported)
			{
				device.ExposurePointOfInterest = devicePoint;
				if (device.IsExposureModeSupported(AVCaptureExposureMode.AutoExpose))
				{
					device.ExposureMode = AVCaptureExposureMode.AutoExpose;
				}
			}
		}
		finally
		{
			device.UnlockForConfiguration();
		}
	}

	private void Fail(string message)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			SetTorch(false);
			DismissViewController(true, () => completion.TrySetException(new InvalidOperationException(message)));
		});
	}

	private void SetTorch(bool enabled)
	{
		var device = captureDevice;
		if (device?.HasTorch != true)
		{
			return;
		}

		NSError? error;
		if (!device.LockForConfiguration(out error))
		{
			return;
		}

		try
		{
			device.TorchMode = enabled ? AVCaptureTorchMode.On : AVCaptureTorchMode.Off;
			torchButton.SetTitle(enabled ? "手电筒开" : "手电筒", UIControlState.Normal);
			torchButton.BackgroundColor = enabled ? UIColor.FromRGB(227, 246, 251).ColorWithAlpha(0.9f) : UIColor.Black.ColorWithAlpha(0.42f);
			torchButton.SetTitleColor(enabled ? UIColor.Black : UIColor.White, UIControlState.Normal);
		}
		finally
		{
			device.UnlockForConfiguration();
		}
	}
}
