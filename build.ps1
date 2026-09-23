[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$buildPath = Join-Path $repoRoot 'build'
$distPath = Join-Path $repoRoot 'dist'

$cscCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$csc = $cscCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if (-not $csc) {
    throw 'The .NET Framework C# compiler was not found.'
}

if (Test-Path -LiteralPath $buildPath -PathType Container) {
    Remove-Item -LiteralPath $buildPath -Recurse -Force
}
New-Item -ItemType Directory -Path $buildPath -Force | Out-Null
New-Item -ItemType Directory -Path $distPath -Force | Out-Null

$iconGenerator = Join-Path $buildPath 'GenerateTrayIcon.exe'
$trayIcon = Join-Path $distPath 'tray-menu.ico'
$launcher = Join-Path $distPath 'DesktopMenuLauncher.exe'
$manifest = Join-Path $repoRoot 'src\DesktopMenuLauncher.manifest'
$launcherSource = Join-Path $repoRoot 'src\TrayLauncher.cs'
$iconGeneratorSource = Join-Path $repoRoot 'tools\GenerateTrayIcon.cs'

& $csc /nologo /target:exe "/out:$iconGenerator" /reference:System.Drawing.dll $iconGeneratorSource
if ($LASTEXITCODE -ne 0) { throw 'Icon generator compilation failed.' }

& $iconGenerator $trayIcon
if ($LASTEXITCODE -ne 0) { throw 'Tray icon generation failed.' }

& $csc /nologo /target:winexe /optimize+ /platform:anycpu `
    "/win32manifest:$manifest" `
    "/win32icon:$trayIcon" `
    "/out:$launcher" `
    /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll `
    /reference:System.Web.Extensions.dll `
    $launcherSource
if ($LASTEXITCODE -ne 0) { throw 'Launcher compilation failed.' }

Copy-Item -LiteralPath (Join-Path $repoRoot 'config\desktop-menu-config.json') -Destination $distPath -Force
Get-ChildItem -LiteralPath (Join-Path $repoRoot 'scripts') -File | Copy-Item -Destination $distPath -Force

Write-Output "Build complete: $distPath"
