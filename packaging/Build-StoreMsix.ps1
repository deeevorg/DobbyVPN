param(
    [Parameter(Mandatory = $true)][string]$IdentityName,
    [Parameter(Mandatory = $true)][string]$Publisher,
    [Parameter(Mandatory = $true)][string]$PublisherDisplayName,
    [Parameter(Mandatory = $true)][ValidatePattern('^\d+\.\d+\.\d+\.\d+$')][string]$Version
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$outputRoot = Join-Path $projectRoot 'artifacts\store'
$stage = Join-Path $outputRoot ('stage-' + $Version + '-' + [guid]::NewGuid().ToString('N'))
$package = Join-Path $outputRoot ("PingVPN-$Version-x64.msix")

if ($IdentityName -notmatch '^[A-Za-z0-9][A-Za-z0-9.-]*$') {
    throw 'IdentityName must be the exact package name reserved in Partner Center.'
}

New-Item -ItemType Directory -Path $stage -Force | Out-Null
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& dotnet publish (Join-Path $projectRoot 'RismanNG.csproj') -c Release -r win-x64 --self-contained true --no-restore -p:PublishReadyToRun=false -o $stage
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

$assets = Join-Path $stage 'Assets'
New-Item -ItemType Directory -Path $assets -Force | Out-Null
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
foreach ($size in @(44, 50, 150)) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([System.Drawing.Color]::FromArgb(14, 18, 28))
        $brush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(67, 142, 255))
        $font = [System.Drawing.Font]::new('Segoe UI', [float]($size * 0.52), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
        try {
            $graphics.FillEllipse($brush, [float]($size * 0.07), [float]($size * 0.07), [float]($size * 0.86), [float]($size * 0.86))
            [System.Windows.Forms.TextRenderer]::DrawText($graphics, 'P', $font, [System.Drawing.Rectangle]::new(0, 0, $size, $size), [System.Drawing.Color]::White, [System.Windows.Forms.TextFormatFlags]::HorizontalCenter -bor [System.Windows.Forms.TextFormatFlags]::VerticalCenter)
        } finally { $font.Dispose(); $brush.Dispose() }
        $bitmap.Save((Join-Path $assets "Logo$size.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
}

$escapedName = [System.Security.SecurityElement]::Escape($IdentityName)
$escapedPublisher = [System.Security.SecurityElement]::Escape($Publisher)
$escapedDisplay = [System.Security.SecurityElement]::Escape($PublisherDisplayName)
$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
         xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
         xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10"
         xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
         IgnorableNamespaces="uap uap10 rescap">
  <Identity Name="$escapedName" Publisher="$escapedPublisher" Version="$Version" ProcessorArchitecture="x64" />
  <Properties>
    <DisplayName>PingVPN</DisplayName>
    <PublisherDisplayName>$escapedDisplay</PublisherDisplayName>
    <Logo>Assets\Logo50.png</Logo>
  </Properties>
  <Resources><Resource Language="en-us" /></Resources>
  <Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.19045.0" /></Dependencies>
  <Applications>
    <Application Id="PingVPN" Executable="PingVPN.exe" uap10:RuntimeBehavior="packagedClassicApp" uap10:TrustLevel="mediumIL">
      <uap:VisualElements DisplayName="PingVPN" Description="Private connection control"
        Square150x150Logo="Assets\Logo150.png" Square44x44Logo="Assets\Logo44.png" BackgroundColor="#0E121C" />
    </Application>
  </Applications>
  <Capabilities><rescap:Capability Name="runFullTrust" /></Capabilities>
</Package>
"@
[System.IO.File]::WriteAllText((Join-Path $stage 'AppxManifest.xml'), $manifest, [System.Text.UTF8Encoding]::new($false))

$sdkRoot = 'C:\Program Files (x86)\Windows Kits\10\bin'
$makeAppx = Get-ChildItem -Path $sdkRoot -Filter MakeAppx.exe -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
if (-not $makeAppx) {
    throw "Published files are staged at $stage, but MakeAppx.exe is missing. Install the Windows SDK to create the MSIX."
}
& $makeAppx pack /d $stage /p $package /o
if ($LASTEXITCODE -ne 0) { throw 'MakeAppx failed; check the manifest and SDK diagnostics.' }
Write-Host "Created unsigned Store submission candidate: $package"
Write-Host 'Before submission, test it on a clean Windows machine and run Windows App Certification Kit.'
