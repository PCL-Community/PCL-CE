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
if (-not (Test-Path -LiteralPath $pluginAssembly -PathType Leaf)) {
    throw "Plugin assembly was not produced: $pluginAssembly"
}

dotnet run --project (Join-Path $repoRoot 'PCL.Desktop\PCL.Desktop.csproj') `
    -c $Configuration `
    "-p:PclPluginAssembly=$pluginAssembly"
exit $LASTEXITCODE
