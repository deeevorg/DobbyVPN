# Third-party notices

The tracked source contains no third-party runtime package. The Windows test build copies reviewed sing-box and Xray binaries to its `Core` directory. The ignored source copies used by the build are under `tools/sing-box/` and `tools/xray/`.

For the local Windows test setup created on 2026-09-26: sing-box v1.14.2, `sing-box-1.14.2-windows-amd64.zip`, SHA-256 `c2d8bfff918755808781dfdeeb8581b6c91eb3a243d9a7b55483cfc0c0684d32`, downloaded from the official SagerNet GitHub release. The extracted distribution includes its GPL-3.0-or-later `LICENSE` file, which is copied alongside the binary.

The application interoperates with a user-supplied sing-box executable. sing-box is not copied, modified, downloaded, or redistributed by this repository. Before adding it to a release, maintainers must record the exact upstream version, SHA-256 checksum, source location, build instructions, license text, and all required notices here and in the release artifact. Do not infer a license from a filename or web summary; verify it for the exact release.

The bundled `Assets/world-countries.geojson` map geometry is Natural Earth 1:110m Admin 0 country data, downloaded from `nvkelso/natural-earth-vector` on GitHub. Natural Earth data is public domain. The file is copied into the test app's `Data` folder for offline map rendering; IP geolocation lookups still require an internet connection and use ipwho.is.

For the local Windows test setup created on 2026-09-26: Xray-core v26.7.28, `Xray-windows-64.zip`, SHA-256 of the extracted `xray.exe` `1D9674327972A21AFD4C906A7A72BB0856935AA9E0227C87F34F03D11A88BDDF`, downloaded from the official XTLS GitHub release. The test artifact includes `xray.exe` and its MPL-2.0 `LICENSE` file. Xray geo-rule databases and Wintun are intentionally not included because this build does not offer TUN mode or geo-rule routing; the offline Natural Earth country shapes used by the UI map are included separately.

Windows and .NET are trademarks of their respective owners. RismanNG does not claim affiliation with Xray, sing-box, V2Ray, or SagerNet.
