# Meter Recti Native

Native C#/.NET MAUI iOS version of Meter Recti.

This repository is separate from the existing PWA/Capacitor app. The goal is to keep the current app stable while building a native v2 with feature parity.

## Targets

- iOS app built with .NET MAUI.
- Native MQTT over TCP/TLS instead of WebSocket MQTT.
- Full-screen native scanner.
- SQLite history storage.
- CSV export through the iOS share sheet.
- Codemagic unsigned device IPA for Sideloadly installation.

## Identity

- App title: `Meter Recti`
- Bundle ID: `com.meterrecti.native`
- Namespace: `MeterRecti.Native`

## Current Status

Project scaffold is in place. Business logic is intentionally not implemented yet.

See [docs/PRODUCT_SPEC.md](docs/PRODUCT_SPEC.md) for the first product and technical specification.
