using Foundation;
using UIKit;

namespace MeterRecti.Native;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
	{
		ConfigureTabBarAppearance();
		return base.FinishedLaunching(application, launchOptions);
	}

	private static void ConfigureTabBarAppearance()
	{
		UITabBar.Appearance.BarTintColor = UIColor.FromRGB(247, 251, 253);
		UITabBar.Appearance.BackgroundColor = UIColor.FromRGB(247, 251, 253);
		UITabBar.Appearance.TintColor = UIColor.FromRGB(36, 54, 69);
		UITabBar.Appearance.UnselectedItemTintColor = UIColor.FromRGB(101, 118, 132);

		var titleOffset = new UIOffset(0, -12);
		UITabBarItem.Appearance.TitlePositionAdjustment = titleOffset;

		if (OperatingSystem.IsIOSVersionAtLeast(15))
		{
			var appearance = new UITabBarAppearance();
			appearance.ConfigureWithOpaqueBackground();
			appearance.BackgroundColor = UIColor.FromRGB(247, 251, 253);
			appearance.StackedLayoutAppearance.Normal.TitlePositionAdjustment = titleOffset;
			appearance.StackedLayoutAppearance.Selected.TitlePositionAdjustment = titleOffset;
			appearance.InlineLayoutAppearance.Normal.TitlePositionAdjustment = titleOffset;
			appearance.InlineLayoutAppearance.Selected.TitlePositionAdjustment = titleOffset;
			appearance.CompactInlineLayoutAppearance.Normal.TitlePositionAdjustment = titleOffset;
			appearance.CompactInlineLayoutAppearance.Selected.TitlePositionAdjustment = titleOffset;
			UITabBar.Appearance.StandardAppearance = appearance;
			UITabBar.Appearance.ScrollEdgeAppearance = appearance;
		}
	}
}
