# Meter Recti Native Product Spec

## Scope

Meter Recti Native is a standalone C#/.NET MAUI iOS app. It should match the current PWA/Capacitor feature set while using native app patterns for UI, scanning, storage, sharing, and MQTT connectivity.

The existing `meter-recti` repository remains the production-ready PWA/Capacitor version. This repository is the native v2 line and must not replace the PWA version until feature parity is proven on a real iPhone.

## Naming

- Repository: `meter-recti-native`
- Solution namespace: `MeterRecti.Native`
- Bundle ID: `com.meterrecti.native`
- App title: `Meter Recti`

## Screens

### Calibration

- Default tab.
- Shows MQTT connection status.
- Accepts scanned or manual SN input.
- Accepts a numeric meter reading from `0` to `999999999`.
- Opens a full-screen native scanner for SN scanning.
- Starts the MQTT calibration flow.
- Stores successful calibration results in local history.

### MQTT

- Broker host.
- Port.
- TLS toggle.
- Username.
- Password.
- Subscribe topic base.
- Publish topic base.
- Save and connect.
- Disconnect.
- Connection states: disconnected, connecting, connected, reconnecting, offline, error.

### History

- Native list of calibration records.
- Shows SN, METERSUM, and calibration time.
- Supports delete one record.
- Supports clear all with confirmation.
- Exports CSV through the iOS share sheet.

## MQTT

Native MQTT should prefer:

```text
mqtts://broker:8883
```

Plain MQTT remains available for compatibility:

```text
mqtt://broker:1883
```

The calibration flow stays compatible with the PWA version:

```text
subscribeTopic = baseSubscribeTopic + "/" + serialNumber
publishTopic   = basePublishTopic + "/" + serialNumber
command        = "D3" + serialNumber + "B2" + meterReading
timeout        = 120000 ms
```

## Visual System

Primary palette from the supplied macaron reference:

```text
Mist       #E3F6FB
Aqua       #C7EEFB
Sky        #ACE6FB
Periwinkle #9BC8FB
```

Supporting palette:

```text
Text       #243645
Subtext    #657684
Divider    #D8E7EE
Surface    #F7FBFD
Success    #2D9B75
Danger     #C6514A
```

The app should feel clean, native, professional, and restrained. Avoid a web-page layout, oversized marketing sections, and heavy decorative gradients.

## Native iOS Capabilities

- Full-screen scanner based on `AVCaptureSession` and `AVCaptureMetadataOutput`.
- Torch toggle.
- Tap to focus.
- Success haptic feedback.
- Optional success sound.
- CSV sharing with `UIActivityViewController`.

## Build Path

The expected distribution flow remains:

```text
GitHub -> Codemagic -> unsigned device IPA -> Sideloadly -> iPhone
```

Free Apple ID sideloading still usually needs renewal after 7 days.
