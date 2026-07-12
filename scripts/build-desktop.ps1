# Copyright (c) 2026 PCL N contributors.
# Reliable local Desktop build — avoids MSB4166 multi-node crashes and file locks.

param(
    [string]$Configuration = "Debug",
    [switch]$Publish,
    [string]$Runtime = "win-x64",
    [switch]$WriteSecrets
)

$ErrorActionPreference = "Stop"

# Stop running app that locks bin outputs.
Get-Process PCL.Desktop -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

$env:MSBUILDDISABLENODEREUSE = "1"
$env:DOTNET_CLI_UI_LANGUAGE = "en"

$project = Join-Path $PSScriptRoot "..\PCL.Desktop\PCL.Desktop.csproj"

if ($WriteSecrets) {
    $env:PCL_WRITE_SECRET = "1"
}

$common = @(
    $project,
    "-c", $Configuration,
    "-m:1",
    "-nodeReuse:false",
    "--nologo"
)

if ($Publish) {
    & dotnet publish @common `
        -r $Runtime `
        --self-contained true `
        -p:PublishAot=false `
        -p:PublishTrimmed=false `
        -p:PublishSingleFile=true `
        -p:PclWriteSecret=1
} else {
    & dotnet build @common
}

exit $LASTEXITCODE
