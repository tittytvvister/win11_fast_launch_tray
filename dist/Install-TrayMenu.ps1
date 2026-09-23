[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$installPath = Join-Path $env:LOCALAPPDATA 'DesktopMenuLauncher'
$startupPath = [Environment]::GetFolderPath('Startup')
$startupShortcut = Join-Path $startupPath 'My Programs.lnk'
$startMenuPath = [Environment]::GetFolderPath('Programs')
$startMenuShortcut = Join-Path $startMenuPath 'My Programs.lnk'
$sourceExe = Join-Path $PSScriptRoot 'DesktopMenuLauncher.exe'
$sourceConfig = Join-Path $PSScriptRoot 'desktop-menu-config.json'
$sourceIcon = Join-Path $PSScriptRoot 'tray-menu.ico'
$installedExe = Join-Path $installPath 'DesktopMenuLauncher.exe'

if (-not (Test-Path -LiteralPath $sourceExe -PathType Leaf)) {
    throw "Launcher not found: $sourceExe"
}

Get-Process -Name 'DesktopMenuLauncher' -ErrorAction SilentlyContinue | Stop-Process -Force
New-Item -ItemType Directory -Path $installPath -Force | Out-Null
Copy-Item -LiteralPath $sourceExe -Destination $installedExe -Force
Copy-Item -LiteralPath $sourceConfig -Destination (Join-Path $installPath 'desktop-menu-config.json') -Force
Copy-Item -LiteralPath $sourceIcon -Destination (Join-Path $installPath 'tray-menu.ico') -Force

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($startupShortcut)
$shortcut.TargetPath = $installedExe
$shortcut.WorkingDirectory = $installPath
$shortcut.IconLocation = "$installedExe,0"
$shortcut.Description = 'My Programs tray launcher'
$shortcut.Save()

$menuShortcut = $shell.CreateShortcut($startMenuShortcut)
$menuShortcut.TargetPath = $installedExe
$menuShortcut.WorkingDirectory = $installPath
$menuShortcut.IconLocation = "$installedExe,0"
$menuShortcut.Description = 'My Programs tray launcher'
$menuShortcut.Save()

$registryPaths = @(
    'Registry::HKEY_CURRENT_USER\Software\Classes\DesktopBackground\Shell\DesktopShortcutMenu',
    'Registry::HKEY_CURRENT_USER\Software\Classes\Directory\Background\shell\MyProgramsMenu'
)
foreach ($registryPath in $registryPaths) {
    if (Test-Path -LiteralPath $registryPath) {
        Remove-Item -LiteralPath $registryPath -Recurse -Force
    }
}

Start-Process -FilePath $installedExe -WorkingDirectory $installPath
Write-Output "Tray menu installed and started: $installedExe"
