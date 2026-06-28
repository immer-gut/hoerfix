$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$dist = Join-Path $root "dist"
$appDist = Join-Path $dist "Hoerfix"
$payloadDir = Join-Path $root "installer\payload"
$setupDist = Join-Path $dist "Setup"
$packagePath = Join-Path $dist "Hoerfix-win-x64.zip"

function Invoke-Checked {
    param([scriptblock]$Command)
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE"
    }
}

if (Test-Path -LiteralPath $dist) {
    Remove-Item -LiteralPath $dist -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $appDist, $payloadDir, $setupDist | Out-Null

Invoke-Checked {
    dotnet publish (Join-Path $root "hoerhilfe.csproj") `
        -c Release `
        -r win-x64 `
        -p:PublishSingleFile=true `
        --self-contained false `
        -o $appDist
}

Copy-Item -LiteralPath (Join-Path $appDist "Hoerfix.exe") -Destination (Join-Path $payloadDir "Hoerfix.exe") -Force

Invoke-Checked {
    dotnet publish (Join-Path $root "installer\Hoerfix.Setup.csproj") `
        -c Release `
        -r win-x64 `
        -p:PublishSingleFile=true `
        --self-contained false `
        -o $setupDist
}

Copy-Item -LiteralPath (Join-Path $setupDist "Hoerfix-Setup.exe") -Destination (Join-Path $dist "Hoerfix-Setup.exe") -Force
Copy-Item -LiteralPath (Join-Path $root "setup\Install-Hoerfix.ps1"), (Join-Path $root "setup\Uninstall-Hoerfix.ps1"), (Join-Path $root "setup\README.md") -Destination $appDist -Force

Compress-Archive -Path (Join-Path $appDist "*") -DestinationPath $packagePath -Force

Write-Host "Release erstellt:"
Write-Host "  $($dist)\Hoerfix-Setup.exe"
Write-Host "  $packagePath"
