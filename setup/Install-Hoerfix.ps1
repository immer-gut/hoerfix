param(
    [string]$InstallDir = "$env:LOCALAPPDATA\Programs\Hoerfix",
    [switch]$NoDesktopShortcut
)

$ErrorActionPreference = "Stop"

$sourceDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$exeSource = Join-Path $sourceDir "Hoerfix.exe"

if (-not (Test-Path -LiteralPath $exeSource)) {
    throw "Hoerfix.exe wurde neben diesem Setup-Skript nicht gefunden."
}

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item -LiteralPath $exeSource -Destination (Join-Path $InstallDir "Hoerfix.exe") -Force

$shell = New-Object -ComObject WScript.Shell
$startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Hoerfix"
New-Item -ItemType Directory -Force -Path $startMenuDir | Out-Null

$startShortcut = $shell.CreateShortcut((Join-Path $startMenuDir "Hoerfix.lnk"))
$startShortcut.TargetPath = Join-Path $InstallDir "Hoerfix.exe"
$startShortcut.WorkingDirectory = $InstallDir
$startShortcut.Description = "Hoerfix starten"
$startShortcut.Save()

if (-not $NoDesktopShortcut) {
    $desktopShortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath("Desktop")) "Hoerfix.lnk"))
    $desktopShortcut.TargetPath = Join-Path $InstallDir "Hoerfix.exe"
    $desktopShortcut.WorkingDirectory = $InstallDir
    $desktopShortcut.Description = "Hoerfix starten"
    $desktopShortcut.Save()
}

Write-Host "Hoerfix wurde installiert: $InstallDir"
