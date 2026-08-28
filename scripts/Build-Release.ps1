$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$dist = Join-Path $root "dist"
$appDist = Join-Path $dist "Hoerfix"
$payloadDir = Join-Path $root "installer\payload"
$setupDist = Join-Path $dist "Setup"
$setupExePath = Join-Path $dist "Hoerfix-Setup.exe"

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
        -p:IncludeNativeLibrariesForSelfExtract=true `
        --self-contained true `
        -o $appDist
}

Copy-Item -LiteralPath (Join-Path $appDist "Hoerfix.exe") -Destination (Join-Path $payloadDir "Hoerfix.exe") -Force

Invoke-Checked {
    dotnet publish (Join-Path $root "installer\Hoerfix.Setup.csproj") `
        -c Release `
        -r win-x64 `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        --self-contained true `
        -o $setupDist
}

Copy-Item -LiteralPath (Join-Path $setupDist "Hoerfix-Setup.exe") -Destination $setupExePath -Force

if (!(Test-Path -LiteralPath $setupExePath)) {
    throw "Setup-EXE wurde nicht erstellt: $setupExePath"
}

Write-Host "Release erstellt:"
Write-Host "  $setupExePath"
