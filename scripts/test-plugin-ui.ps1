param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
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
$pluginBouncyCastleAssembly = Join-Path $pluginDirectory "bin\$Configuration\net10.0\BouncyCastle.Cryptography.dll"
$pluginJsonCanonicalizerAssembly = Join-Path $pluginDirectory "bin\$Configuration\net10.0\jsoncanonicalizer.dll"
$pluginEs6NumberSerializerAssembly = Join-Path $pluginDirectory "bin\$Configuration\net10.0\es6numberserializer.dll"
foreach ($assembly in @($pluginAssembly, $pluginAbstractionsAssembly, $pluginBouncyCastleAssembly, $pluginJsonCanonicalizerAssembly, $pluginEs6NumberSerializerAssembly)) {
    if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) {
        throw "Plugin assembly was not produced: $assembly"
    }
}

$previousExpectation = $env:PCLN_EXPECT_PLUGIN_UI
$previousRuntimePath = $env:PCLN_PLUGIN_RUNTIME_PATH
$isolatedRuntimePath = Join-Path ([System.IO.Path]::GetTempPath()) ('pcl-plugin-ui-test-' + [Guid]::NewGuid().ToString('N'))
try {
    $env:PCLN_EXPECT_PLUGIN_UI = '1'
    $env:PCLN_PLUGIN_RUNTIME_PATH = $isolatedRuntimePath
    dotnet test (Join-Path $repoRoot 'PCL.Desktop.Test\PCL.Desktop.Test.csproj') `
        -c $Configuration `
        "-p:PclPluginAssembly=$pluginAssembly" `
        "-p:PclPluginAbstractionsAssembly=$pluginAbstractionsAssembly" `
        "-p:PclPluginBouncyCastleAssembly=$pluginBouncyCastleAssembly" `
        "-p:PclPluginJsonCanonicalizerAssembly=$pluginJsonCanonicalizerAssembly" `
        "-p:PclPluginEs6NumberSerializerAssembly=$pluginEs6NumberSerializerAssembly" `
        --filter 'TestCategory=InjectedPlugin' `
        --blame-hang `
        --blame-hang-timeout 60s `
        --blame-hang-dump-type mini `
        -warnaserror
    exit $LASTEXITCODE
}
finally {
    $env:PCLN_EXPECT_PLUGIN_UI = $previousExpectation
    $env:PCLN_PLUGIN_RUNTIME_PATH = $previousRuntimePath
    if (Test-Path -LiteralPath $isolatedRuntimePath) {
        Remove-Item -LiteralPath $isolatedRuntimePath -Recurse -Force
    }
}
