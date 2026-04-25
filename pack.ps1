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
$nugetSource = 'https://api.nuget.org/v3/index.json'

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
