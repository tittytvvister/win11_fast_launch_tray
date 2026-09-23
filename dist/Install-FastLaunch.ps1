[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$installPath = Join-Path $env:LOCALAPPDATA 'FastLaunch'
$legacyInstallPath = Join-Path $env:LOCALAPPDATA 'DesktopMenuLauncher'
$startupPath = [Environment]::GetFolderPath('Startup')
$startupShortcut = Join-Path $startupPath 'Fast Launch.lnk'
$legacyStartupShortcut = Join-Path $startupPath 'My Programs.lnk'
$startMenuPath = [Environment]::GetFolderPath('Programs')
$startMenuShortcut = Join-Path $startMenuPath 'Fast Launch.lnk'
$legacyStartMenuShortcut = Join-Path $startMenuPath 'My Programs.lnk'
$sourceExe = Join-Path $PSScriptRoot 'FastLaunch.exe'
$sourceConfig = Join-Path $PSScriptRoot 'fast-launch-config.json'
$sourceIcon = Join-Path $PSScriptRoot 'fast-launch.ico'
$installedExe = Join-Path $installPath 'FastLaunch.exe'
$installedIcon = Join-Path $installPath 'fast-launch.ico'

if (-not (Test-Path -LiteralPath $sourceExe -PathType Leaf)) {
    throw "Launcher not found: $sourceExe"
}

Get-Process -Name 'FastLaunch','DesktopMenuLauncher' -ErrorAction SilentlyContinue | Stop-Process -Force
New-Item -ItemType Directory -Path $installPath -Force | Out-Null
Copy-Item -LiteralPath $sourceExe -Destination $installedExe -Force
Copy-Item -LiteralPath $sourceConfig -Destination (Join-Path $installPath 'fast-launch-config.json') -Force
Copy-Item -LiteralPath $sourceIcon -Destination $installedIcon -Force

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($startupShortcut)
$shortcut.TargetPath = $installedExe
$shortcut.WorkingDirectory = $installPath
$shortcut.IconLocation = "$installedIcon,0"
$shortcut.Description = 'Fast Launch'
$shortcut.Save()

$menuShortcut = $shell.CreateShortcut($startMenuShortcut)
$menuShortcut.TargetPath = $installedExe
$menuShortcut.WorkingDirectory = $installPath
$menuShortcut.IconLocation = "$installedIcon,0"
$menuShortcut.Description = 'Fast Launch'
$menuShortcut.Save()

foreach ($legacyShortcut in @($legacyStartupShortcut, $legacyStartMenuShortcut)) {
    if (Test-Path -LiteralPath $legacyShortcut -PathType Leaf) {
        Remove-Item -LiteralPath $legacyShortcut -Force
    }
}
if (Test-Path -LiteralPath $legacyInstallPath -PathType Container) {
    Remove-Item -LiteralPath $legacyInstallPath -Recurse -Force
}

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
Write-Output "Fast Launch installed and started: $installedExe"
