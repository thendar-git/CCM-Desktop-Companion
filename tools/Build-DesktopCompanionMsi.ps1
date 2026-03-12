param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$appProj = Join-Path $root 'CCM.DesktopCompanion\CCM.DesktopCompanion.csproj'
$installerDir = Join-Path $root 'Installer'
$installerProj = Join-Path $installerDir 'CCM.DesktopCompanion.Installer.wixproj'
$publishDir = Join-Path $root 'artifacts\publish\'
$msiOutDir = Join-Path $root 'Builds\'

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
New-Item -ItemType Directory -Force -Path $msiOutDir | Out-Null

Write-Host 'Publishing desktop companion...'
dotnet publish $appProj -c $Configuration -r win-x64 --self-contained true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

Write-Host 'Generating WiX includes...'
$componentsPath = Join-Path $installerDir 'GeneratedComponents.wxi'
$refsPath = Join-Path $installerDir 'GeneratedComponentRefs.wxi'

$files = Get-ChildItem -Path $publishDir -File | Sort-Object Name
if (-not $files) { throw 'No published files found.' }

$componentLines = New-Object System.Collections.Generic.List[string]
$refLines = New-Object System.Collections.Generic.List[string]
$componentLines.Add('<Include xmlns="http://wixtoolset.org/schemas/v4/wxs">')
$refLines.Add('<Include xmlns="http://wixtoolset.org/schemas/v4/wxs">')
$index = 1
foreach ($file in $files) {
    $id = 'Cmp' + $index
    $fileId = 'Fil' + $index
    $guid = [guid]::NewGuid().ToString().ToUpperInvariant()
    $escapedName = [System.Security.SecurityElement]::Escape($file.Name)
    $escapedPath = [System.Security.SecurityElement]::Escape($file.FullName)
    $componentLines.Add("  <Component Id=`"$id`" Guid=`"$guid`">")
    $componentLines.Add("    <File Id=`"$fileId`" Source=`"$escapedPath`" Name=`"$escapedName`" KeyPath=`"no`" />")
    $componentLines.Add("    <RegistryValue Root=`"HKCU`" Key=`"Software\Thendar\CCMDesktopCompanion\Components`" Name=`"$id`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`" />")
    $componentLines.Add('  </Component>')
    $refLines.Add("  <ComponentRef Id=`"$id`" />")
    $index++
}
$componentLines.Add('</Include>')
$refLines.Add('</Include>')

[System.IO.File]::WriteAllLines($componentsPath, $componentLines)
[System.IO.File]::WriteAllLines($refsPath, $refLines)

Write-Host 'Building MSI...'
dotnet build $installerProj -c $Configuration -o $msiOutDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet build installer failed.' }

Write-Host "MSI output:`n$msiOutDir"

