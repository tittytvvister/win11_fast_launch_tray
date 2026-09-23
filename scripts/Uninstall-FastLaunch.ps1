[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$installPath = Join-Path $env:LOCALAPPDATA 'FastLaunch'
$legacyInstallPath = Join-Path $env:LOCALAPPDATA 'DesktopMenuLauncher'
$startupShortcut = Join-Path ([Environment]::GetFolderPath('Startup')) 'Fast Launch.lnk'
$legacyStartupShortcut = Join-Path ([Environment]::GetFolderPath('Startup')) 'My Programs.lnk'
$startMenuShortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Fast Launch.lnk'
$legacyStartMenuShortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'My Programs.lnk'

Get-Process -Name 'FastLaunch','DesktopMenuLauncher' -ErrorAction SilentlyContinue | Stop-Process -Force
foreach ($shortcut in @($startupShortcut, $legacyStartupShortcut, $startMenuShortcut, $legacyStartMenuShortcut)) {
    if (Test-Path -LiteralPath $shortcut -PathType Leaf) {
        Remove-Item -LiteralPath $shortcut -Force
    }
}
foreach ($path in @($installPath, $legacyInstallPath)) {
    if (Test-Path -LiteralPath $path -PathType Container) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

Write-Output 'Fast Launch removed.'
