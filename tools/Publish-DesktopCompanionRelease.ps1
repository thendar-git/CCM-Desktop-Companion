param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$appProj = Join-Path $root 'CCM.DesktopCompanion\CCM.DesktopCompanion.csproj'
$buildScript = Join-Path $PSScriptRoot 'Build-DesktopCompanionMsi.ps1'
$msiPath = Join-Path $root 'Builds\CCM.DesktopCompanion.Installer.msi'
$owner = 'thendar-git'
$repo = 'CCM-Desktop-Companion'

[xml]$projectXml = Get-Content -Path $appProj
$version = $projectXml.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'Unable to determine application version from CCM.DesktopCompanion.csproj.'
}
$tag = "v$version"
$releaseName = $tag

& $buildScript -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Build-DesktopCompanionMsi.ps1 failed.' }
if (-not (Test-Path -Path $msiPath)) {
    throw "MSI not found at $msiPath"
}

$tagExists = $false
try {
    git -C $root rev-parse --verify --quiet "refs/tags/$tag" | Out-Null
    $tagExists = ($LASTEXITCODE -eq 0)
} catch {
    $tagExists = $false
}
if (-not $tagExists) {
    git -C $root tag $tag
    if ($LASTEXITCODE -ne 0) { throw "Failed to create git tag $tag" }
}

git -C $root push origin master --follow-tags
if ($LASTEXITCODE -ne 0) { throw 'git push failed.' }

$credInput = "protocol=https`nhost=github.com`nusername=thendar-git`n"
$cred = $credInput | git credential-manager get
$tokenMatch = [regex]::Match($cred, '^password=(.+)$', [System.Text.RegularExpressions.RegexOptions]::Multiline)
if (-not $tokenMatch.Success) {
    throw 'Unable to retrieve GitHub token from git credential-manager.'
}
$token = $tokenMatch.Groups[1].Value
$headers = @{
    Authorization = "Bearer $token"
    Accept = 'application/vnd.github+json'
    'User-Agent' = 'CCM-DesktopCompanion-ReleaseScript'
}
$apiBase = "https://api.github.com/repos/$owner/$repo"

$release = $null
try {
    $release = Invoke-RestMethod -Method Get -Headers $headers -Uri "$apiBase/releases/tags/$tag"
} catch {
    if ($_.Exception.Response.StatusCode.value__ -ne 404) {
        throw
    }
}

if (-not $release) {
    $body = @{
        tag_name = $tag
        target_commitish = 'master'
        name = $releaseName
        draft = $false
        prerelease = $false
        generate_release_notes = $false
        body = "Release $tag"
    } | ConvertTo-Json
    $release = Invoke-RestMethod -Method Post -Headers $headers -Uri "$apiBase/releases" -Body $body -ContentType 'application/json'
}

$assetName = [System.IO.Path]::GetFileName($msiPath)
$existingAsset = $release.assets | Where-Object { $_.name -eq $assetName } | Select-Object -First 1
if ($existingAsset) {
    Invoke-RestMethod -Method Delete -Headers $headers -Uri "$apiBase/releases/assets/$($existingAsset.id)"
}

$uploadHeaders = @{
    Authorization = "Bearer $token"
    Accept = 'application/vnd.github+json'
    'User-Agent' = 'CCM-DesktopCompanion-ReleaseScript'
    'Content-Type' = 'application/x-msi'
}
$uploadUrl = "https://uploads.github.com/repos/$owner/$repo/releases/$($release.id)/assets?name=$([uri]::EscapeDataString($assetName))"
Invoke-RestMethod -Method Post -Headers $uploadHeaders -Uri $uploadUrl -InFile $msiPath

Write-Host "Published $tag with asset $assetName"
