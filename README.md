# PingVPN

PingVPN is a free, local-first Windows desktop client for user-owned V2Ray-compatible links. It does not require an account, collect telemetry, or include a hosted service.

## Install

[Get PingVPN from the Microsoft Store](https://apps.microsoft.com/detail/9p9wng7n8wbm)

**Known issue in Store version 1.0.1.0:** automatic Windows system proxy activation may fail even when the app reports Connected. Applications relying on the system proxy may therefore remain outside the connection. A fix is pending.

## Current scope

The implemented path is: import a supported profile link, HTTPS subscription URL, or sing-box JSON, select a profile, then connect/disconnect. Use **Manage profiles** to edit names, connection fields, options, or raw sing-box JSON, replace one profile with an updated link, set the active profile, or delete a profile. Disconnect before editing or deleting. Use **Subscriptions** to add a URL and manually refresh it; the clipboard import button also accepts an HTTPS subscription URL. Refresh replaces that subscription's profiles instead of appending duplicates, so edits to a subscription profile may be overwritten and deleted subscription profiles may return on refresh. The Windows test build includes reviewed Xray-core and sing-box binaries, so end users do not need a separate installation or an executable path. VLESS, VMess, Trojan and Shadowsocks select Xray-core; Hysteria2 and direct sing-box JSON select sing-box. The app verifies an external IP through the selected proxy before reporting Connected and checks the route periodically while connected. Connection applies Windows System Proxy on an available loopback port; it records the prior proxy state and only restores it if the user has not changed the proxy in the meantime. The UI is WinForms and defaults to a dark theme. Profile data and subscription URLs are protected using Windows DPAPI under `%LOCALAPPDATA%\RismanNG`.

Supported URL imports: VLESS (TCP/WS/gRPC validation; TLS), VMess, Trojan, Shadowsocks and Hysteria2. Direct sing-box JSON can also be stored and passed to the core. Unsupported query values, transports and Reality generation are rejected rather than guessed. Subscription import accepts HTTPS URLs only, has a 20-second timeout and a 5 MB response cap. Valid links in a mixed subscription are imported while unsupported links are counted and skipped.

## Important limitations

- The test build bundles the exact Xray-core and sing-box versions listed in `THIRD-PARTY-NOTICES.md`; a production release needs a maintained update and license-review process for both.
- Tray integration, latency checks, scheduled subscription refresh, startup, controlled restart, and TUN are not implemented in this initial source drop. TUN is explicitly not presented as ready.
- The generated outbound mapping is deliberately conservative. Validate every configuration with the exact sing-box version you use before relying on it.
- The application does not claim leak prevention, censorship bypass, performance targets, or a fixed package/RAM size.

## Build and run

This repository targets Windows and needs the .NET 8 SDK (a runtime alone is insufficient):

```powershell
dotnet restore
dotnet build -c Release
dotnet run --project .\RismanNG.csproj
```

For a self-contained build, only after measuring and reviewing its licenses:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Security

Profile secrets and subscription data must be treated as sensitive. Avoid sharing logs or screenshots. Do not run an untrusted sing-box executable or import untrusted configuration JSON. See [SECURITY.md](SECURITY.md).

## License and distribution

PingVPN source is released under the Unlicense; see LICENSE. The Windows test build includes sing-box as a separate GPL-3.0-or-later program together with its license. A future public release must retain the exact version, checksum, notices, source location, and reproducible build instructions. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). This project is unaffiliated with V2Ray, sing-box, or SagerNet.
