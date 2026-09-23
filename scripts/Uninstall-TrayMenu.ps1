[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$installPath = Join-Path $env:LOCALAPPDATA 'DesktopMenuLauncher'
$startupShortcut = Join-Path ([Environment]::GetFolderPath('Startup')) 'My Programs.lnk'
$startMenuShortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'My Programs.lnk'

Get-Process -Name 'DesktopMenuLauncher' -ErrorAction SilentlyContinue | Stop-Process -Force
if (Test-Path -LiteralPath $startupShortcut -PathType Leaf) {
    Remove-Item -LiteralPath $startupShortcut -Force
}
if (Test-Path -LiteralPath $startMenuShortcut -PathType Leaf) {
    Remove-Item -LiteralPath $startMenuShortcut -Force
}
if (Test-Path -LiteralPath $installPath -PathType Container) {
    Remove-Item -LiteralPath $installPath -Recurse -Force
}

Write-Output 'Tray menu removed.'
