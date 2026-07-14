param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [string]$PluginProject
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($PluginProject)) {
    $PluginProject = Join-Path $repoRoot 'PCL.Plugin\PCL.Plugin.csproj'
}
$PluginProject = [System.IO.Path]::GetFullPath($PluginProject)
if (-not (Test-Path -LiteralPath $PluginProject -PathType Leaf)) {
    throw "PCL.Plugin project not found: $PluginProject. Clone the private plugin repository into PCL.Plugin or pass -PluginProject."
}

dotnet build $PluginProject -c $Configuration "-p:PclNRoot=$repoRoot" -warnaserror
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$pluginDirectory = Split-Path -Parent $PluginProject
$pluginAssembly = Join-Path $pluginDirectory "bin\$Configuration\net10.0\PCL.Plugin.dll"
$pluginAbstractionsAssembly = Join-Path $pluginDirectory "bin\$Configuration\net10.0\PCL.N.Plugin.Abstractions.dll"
$pluginUiAssembly = Join-Path $pluginDirectory "bin\$Configuration\net10.0\PCL.N.Plugin.UI.dll"
$pluginUiAvaloniaAssembly = Join-Path $pluginDirectory "bin\$Configuration\net10.0\PCL.N.Plugin.UI.Avalonia.dll"
$pluginBouncyCastleAssembly = Join-Path $pluginDirectory "bin\$Configuration\net10.0\BouncyCastle.Cryptography.dll"
$pluginJsonCanonicalizerAssembly = Join-Path $pluginDirectory "bin\$Configuration\net10.0\jsoncanonicalizer.dll"
$pluginEs6NumberSerializerAssembly = Join-Path $pluginDirectory "bin\$Configuration\net10.0\es6numberserializer.dll"
foreach ($assembly in @($pluginAssembly, $pluginAbstractionsAssembly, $pluginUiAssembly, $pluginUiAvaloniaAssembly, $pluginBouncyCastleAssembly, $pluginJsonCanonicalizerAssembly, $pluginEs6NumberSerializerAssembly)) {
    if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) {
        throw "Plugin assembly was not produced: $assembly"
    }
}

dotnet run --project (Join-Path $repoRoot 'PCL.Desktop\PCL.Desktop.csproj') `
    -c $Configuration `
    "-p:PclPluginAssembly=$pluginAssembly" `
    "-p:PclPluginAbstractionsAssembly=$pluginAbstractionsAssembly" `
    "-p:PclPluginUiAssembly=$pluginUiAssembly" `
    "-p:PclPluginUiAvaloniaAssembly=$pluginUiAvaloniaAssembly" `
    "-p:PclPluginBouncyCastleAssembly=$pluginBouncyCastleAssembly" `
    "-p:PclPluginJsonCanonicalizerAssembly=$pluginJsonCanonicalizerAssembly" `
    "-p:PclPluginEs6NumberSerializerAssembly=$pluginEs6NumberSerializerAssembly"
exit $LASTEXITCODE
