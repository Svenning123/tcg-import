<#
.SYNOPSIS
    Builds artifacts\TcgImport-Setup-<version>.exe, an installer wizard for TCG Import.
.DESCRIPTION
    Publishes the app self-contained for 64-bit Windows (so friends don't need .NET installed),
    then compiles installer\TcgImport.iss with Inno Setup 6.
    Install Inno Setup once with: winget install --id JRSoftware.InnoSetup -e --scope user
#>
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'src\TcgImport.App\TcgImport.App.csproj'
$publishDir = Join-Path $root 'artifacts\publish'

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    throw 'Inno Setup 6 not found. Install it with: winget install --id JRSoftware.InnoSetup -e --scope user'
}

$version = (dotnet msbuild $project -getProperty:Version).Trim()

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $project -c Release -r win-x64 --self-contained true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

& $iscc "/DAppVersion=$version" "/DPublishDir=$publishDir" (Join-Path $root 'installer\TcgImport.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed.' }

Write-Host "Installer: $(Join-Path $root "artifacts\TcgImport-Setup-$version.exe")"
