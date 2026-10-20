param([string]$GamePath = $env:VINTAGE_STORY)
$ErrorActionPreference = "Stop"
if (-not $GamePath -and $env:APPDATA) { $GamePath = Join-Path $env:APPDATA "Vintagestory" }
if (-not $GamePath -or -not (Test-Path (Join-Path $GamePath "VintagestoryAPI.dll"))) {
    throw 'Specify your game installation folder: .\Build.ps1 -GamePath "C:\path\to\Vintagestory"'
}
$null = Get-Command dotnet -ErrorAction Stop
$root = $PSScriptRoot
$output = Join-Path $root "build\compiled"
$stage = Join-Path $root "build\package"
$releases = Join-Path $root "Releases"
& dotnet build (Join-Path $root "OobeRadio.csproj") -c Release "-p:GamePath=$GamePath" -o $output
if ($LASTEXITCODE -ne 0) { throw "Build failed. Read the first error above." }
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item $stage -ItemType Directory -Force | Out-Null
New-Item $releases -ItemType Directory -Force | Out-Null
foreach ($name in @("OobeRadio.dll", "NLayer.dll")) { Copy-Item (Join-Path $output $name) $stage }
foreach ($name in @("modinfo.json", "modicon.png", "assets", "licenses")) {
    Copy-Item (Join-Path $root $name) $stage -Recurse
}
$version = (Get-Content (Join-Path $root "modinfo.json") -Raw | ConvertFrom-Json).version
$archive = Join-Path $releases "ooberadio_$version.zip"
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $archive -Force
Write-Host "Ready to install: $archive"
