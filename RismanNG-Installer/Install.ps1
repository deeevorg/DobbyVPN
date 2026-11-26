$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

$installRoot = Join-Path $env:LOCALAPPDATA 'Programs\RismanNG'
$extractRoot = Join-Path $env:TEMP ("RismanNG-Setup-" + [guid]::NewGuid().ToString('N'))
$menuRoot = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\RismanNG'
$desktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'RismanNG.lnk'

try {
    if (-not [Environment]::Is64BitOperatingSystem) {
        [System.Windows.Forms.MessageBox]::Show('RismanNG test build requires 64-bit Windows.', 'RismanNG Setup', 'OK', 'Error') | Out-Null
        exit 1
    }
    if (Get-Process -Name RismanNG -ErrorAction SilentlyContinue) {
        [System.Windows.Forms.MessageBox]::Show('Please close RismanNG before installing or updating it.', 'RismanNG Setup', 'OK', 'Warning') | Out-Null
        exit 1
    }

    New-Item -ItemType Directory -Path $extractRoot -Force | Out-Null
    Expand-Archive -LiteralPath (Join-Path $PSScriptRoot 'payload.zip') -DestinationPath $extractRoot -Force
    $sourceRoot = $extractRoot
    if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot 'RismanNG.exe'))) { throw 'The application payload is incomplete.' }

    New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
    Copy-Item -Path (Join-Path $sourceRoot '*') -Destination $installRoot -Recurse -Force

    New-Item -ItemType Directory -Path $menuRoot -Force | Out-Null
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut((Join-Path $menuRoot 'RismanNG.lnk'))
    $shortcut.TargetPath = Join-Path $installRoot 'RismanNG.exe'
    $shortcut.WorkingDirectory = $installRoot
    $shortcut.IconLocation = Join-Path $installRoot 'RismanNG.exe'
    $shortcut.Save()
    $desktop = $shell.CreateShortcut($desktopShortcut)
    $desktop.TargetPath = Join-Path $installRoot 'RismanNG.exe'
    $desktop.WorkingDirectory = $installRoot
    $desktop.IconLocation = Join-Path $installRoot 'RismanNG.exe'
    $desktop.Save()

    $uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\RismanNG-Test'
    New-Item -Path $uninstallKey -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name DisplayName -Value 'RismanNG (Test Build)' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name DisplayVersion -Value 'Test build 2026.09.28.2' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name Publisher -Value 'RismanNG' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name InstallLocation -Value $installRoot -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name UninstallString -Value ('powershell.exe -NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $installRoot 'Uninstall.ps1') + '"') -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null

    [System.Windows.Forms.MessageBox]::Show('RismanNG was installed for this Windows account. A desktop shortcut has been created. This test build includes Xray-core and sing-box.', 'RismanNG Setup', 'OK', 'Information') | Out-Null
    Start-Process (Join-Path $installRoot 'RismanNG.exe') -WorkingDirectory $installRoot
}
catch {
    [System.Windows.Forms.MessageBox]::Show(('Installation failed: ' + $_.Exception.Message), 'RismanNG Setup', 'OK', 'Error') | Out-Null
    exit 1
}
finally {
    if (Test-Path -LiteralPath $extractRoot) { Remove-Item -LiteralPath $extractRoot -Recurse -Force -ErrorAction SilentlyContinue }
}
