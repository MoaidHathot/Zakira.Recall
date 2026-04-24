[CmdletBinding()]
param(
    [string]$ApiKey,
    [switch]$Push
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolProject = Join-Path $repoRoot 'src\Zakira.Recall.Tool\Zakira.Recall.Tool.csproj'
$packageOutput = Join-Path $repoRoot 'artifacts\packages'
$packageScratch = Join-Path $packageOutput 'scratch'
$nugetSource = 'https://api.nuget.org/v3/index.json'
$secondaryCommandName = 'zakira.recall'

if (-not (Test-Path $toolProject)) {
    throw "Tool project not found: $toolProject"
}

if (-not (Test-Path $packageOutput)) {
    New-Item -ItemType Directory -Path $packageOutput | Out-Null
}

Write-Host "Packing Zakira.Recall tool..."
dotnet pack "$toolProject" -c Release --output "$packageOutput"
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet pack failed.'
}

$package = Get-ChildItem -Path $packageOutput -Filter 'Zakira.Recall.*.nupkg' |
    Where-Object { $_.Name -notlike '*.symbols.nupkg' } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if ($null -eq $package) {
    throw "No package was produced in $packageOutput"
}

if (Test-Path $packageScratch) {
    Remove-Item -Recurse -Force $packageScratch
}

Expand-Archive -LiteralPath $package.FullName -DestinationPath $packageScratch -Force
$toolSettingsPath = Join-Path $packageScratch 'tools\net10.0\any\DotnetToolSettings.xml'
if (-not (Test-Path $toolSettingsPath)) {
    throw "DotnetToolSettings.xml was not found in package scratch directory: $toolSettingsPath"
}

[xml]$toolSettings = Get-Content -LiteralPath $toolSettingsPath
$commandsNode = $toolSettings.DotNetCliTool.Commands
$primaryCommand = $commandsNode.Command | Select-Object -First 1
if ($null -eq $primaryCommand) {
    throw 'The generated DotnetToolSettings.xml did not contain a primary command.'
}

$aliasExists = @($commandsNode.Command) | Where-Object { $_.Name -eq $secondaryCommandName }
if (-not $aliasExists) {
    $aliasCommand = $toolSettings.CreateElement('Command')
    $aliasCommand.SetAttribute('Name', $secondaryCommandName)
    $aliasCommand.SetAttribute('EntryPoint', $primaryCommand.EntryPoint)
    $aliasCommand.SetAttribute('Runner', $primaryCommand.Runner)
    [void]$commandsNode.AppendChild($aliasCommand)
    $toolSettings.Save($toolSettingsPath)

    $packageName = Split-Path -Leaf $package.FullName
    Remove-Item -LiteralPath $package.FullName -Force
    [System.IO.Compression.ZipFile]::CreateFromDirectory($packageScratch, (Join-Path $packageOutput $packageName))
}

Write-Host "Created package: $($package.FullName)"

if (-not $Push) {
    return
}

if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    $ApiKey = $env:NUGET_API_KEY
}

if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    throw 'NuGet push requested but no API key was provided. Pass -ApiKey or set NUGET_API_KEY.'
}

Write-Host 'Pushing package to NuGet.org...'
dotnet nuget push "$($package.FullName)" --source $nugetSource --api-key $ApiKey --skip-duplicate
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet nuget push failed.'
}

Write-Host 'Push complete.'
