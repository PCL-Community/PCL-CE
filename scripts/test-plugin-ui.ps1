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
if (-not (Test-Path -LiteralPath $pluginAssembly -PathType Leaf)) {
    throw "Plugin assembly was not produced: $pluginAssembly"
}
if (-not (Test-Path -LiteralPath $pluginAbstractionsAssembly -PathType Leaf)) {
    throw "Plugin abstractions assembly was not produced: $pluginAbstractionsAssembly"
}

$previousExpectation = $env:PCLN_EXPECT_PLUGIN_UI
try {
    $env:PCLN_EXPECT_PLUGIN_UI = '1'
    dotnet test (Join-Path $repoRoot 'PCL.Desktop.Test\PCL.Desktop.Test.csproj') `
        -c $Configuration `
        "-p:PclPluginAssembly=$pluginAssembly" `
        "-p:PclPluginAbstractionsAssembly=$pluginAbstractionsAssembly" `
        --filter 'FullyQualifiedName~InjectedPlugin_RegistersSettingsPageInHeadlessUi' `
        --blame-hang `
        --blame-hang-timeout 120s `
        -warnaserror
    exit $LASTEXITCODE
}
finally {
    $env:PCLN_EXPECT_PLUGIN_UI = $previousExpectation
}
