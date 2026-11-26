# Microsoft Store package

`Build-StoreMsix.ps1` publishes a self-contained x64 WinForms build and packages it as an unsigned MSIX. Install the Windows 10/11 SDK (which provides MakeAppx.exe) and restore the project's .NET dependencies before running it.

```powershell
.\packaging\Build-StoreMsix.ps1 -IdentityName 'YOUR_RESERVED_PARTNER_CENTER_IDENTITY' -Publisher 'CN=YOUR_PARTNER_CENTER_PUBLISHER' -PublisherDisplayName 'YOUR_PUBLISHER' -Version '1.0.0.0'
```

Get `IdentityName` and `Publisher` from the app identity page in Partner Center; they must match exactly. The output is `artifacts\store\PingVPN-<version>-x64.msix`. Store submission signs the certified MSIX; unsigned output is not directly installable for local testing. For local testing, sign it with a trusted test certificate matching the manifest Publisher, or use Visual Studio's packaging workflow.

Before Store submission, verify system proxy changes and restoration, both bundled cores, tray behavior, profile persistence, upgrades, clean uninstall, and network/privacy disclosures on a clean Windows 10/11 x64 device. Run Windows App Certification Kit. This script does not submit to Partner Center and does not replace the existing portable ZIP.
