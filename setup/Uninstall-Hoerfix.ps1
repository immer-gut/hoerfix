param(
    [string]$InstallDir = "$env:LOCALAPPDATA\Programs\Hoerfix"
)

$ErrorActionPreference = "Stop"

$startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Hoerfix"
$desktopShortcut = Join-Path ([Environment]::GetFolderPath("Desktop")) "Hoerfix.lnk"

if (Test-Path -LiteralPath $startMenuDir) {
    Remove-Item -LiteralPath $startMenuDir -Recurse -Force
}

if (Test-Path -LiteralPath $desktopShortcut) {
    Remove-Item -LiteralPath $desktopShortcut -Force
}

if (Test-Path -LiteralPath $InstallDir) {
    Remove-Item -LiteralPath $InstallDir -Recurse -Force
}

Write-Host "Hoerfix wurde entfernt."
