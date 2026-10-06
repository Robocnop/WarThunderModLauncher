<#
.SYNOPSIS
  Publishes a "content-*" GitHub release: the mod files + manifest.json the launcher reads.

.EXAMPLE
  ./tools/publish-content.ps1 -Tag content-2026.10 -Files C:\path\RCSM_Build_September_7_2026.zip, C:\path\ConfigRobo2026Octobre.blk

  The manifest is src/WTModLauncher/default-manifest.json: update versions/urls/sha256 there first
  (this script refuses to publish if a file's SHA-256 or size does not match the manifest).

.EXAMPLE
  ./tools/publish-content.ps1 -Tag content-2026.10 -Files C:\path\RCSM_Build_September_7_2026.zip -ManifestOnly

  Checks the files against the manifest but only re-uploads manifest.json (e.g. after adding metadata).
#>
param(
    [Parameter(Mandatory)] [string] $Tag,
    [Parameter(Mandatory)] [string[]] $Files,
    [string] $Repo = "Robocnop/WarThunderModLauncher",
    [switch] $Prerelease,
    [switch] $ManifestOnly
)
$ErrorActionPreference = "Stop"

$manifestPath = Join-Path $PSScriptRoot "..\src\WTModLauncher\default-manifest.json"
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json

foreach ($file in $Files) {
    $name = Split-Path $file -Leaf
    $item = $manifest.items | Where-Object { $_.fileName -eq $name }
    if (-not $item) { throw "$name is not referenced by default-manifest.json" }
    $hash = (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $item.sha256) { throw "$name sha256 $hash != manifest $($item.sha256)" }
    if ((Get-Item $file).Length -ne $item.size) { throw "$name size mismatch with manifest" }
    if ($item.type -eq "soundmod") {
        # "files" (bank name -> size) lets the launcher recognise a copy installed by hand.
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path $file))
        try {
            $banks = @{}
            foreach ($e in $zip.Entries) { if ($e.Name -like "*.bank") { $banks[$e.Name] = $e.Length } }
        } finally { $zip.Dispose() }
        $expected = @{}
        if ($item.files) { $item.files.PSObject.Properties | ForEach-Object { $expected[$_.Name] = [long]$_.Value } }
        $wrong = $banks.Keys | Where-Object { $expected[$_] -ne $banks[$_] }
        if ($banks.Count -ne $expected.Count -or $wrong) {
            $json = ($banks.GetEnumerator() | Sort-Object Name | ForEach-Object { "        `"$($_.Name)`": $($_.Value)" }) -join ",`n"
            throw "$name 'files' in the manifest does not match the banks in the zip. Use:`n      `"files`": {`n$json`n      }"
        }
    }
    $expectedUrl = "https://github.com/$Repo/releases/download/$Tag/$name"
    if ($item.url -ne $expectedUrl) { throw "$name url in manifest should be $expectedUrl" }
    Write-Host "OK  $name  $hash"
}

$tmpManifest = Join-Path ([IO.Path]::GetTempPath()) "manifest.json"
Copy-Item $manifestPath $tmpManifest -Force

gh release view $Tag --repo $Repo 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    $notes = "Mods et config distribués par WT Mod Launcher. Ne pas télécharger à la main : utilise le launcher."
    $createArgs = @("release", "create", $Tag, "--repo", $Repo, "--target", "main", "--title", "Contenu $Tag", "--notes", $notes)
    if ($Prerelease) { $createArgs += "--prerelease" }
    & gh @createArgs
    if ($LASTEXITCODE -ne 0) { throw "gh release create failed" }
}
$upload = @($tmpManifest)
if (-not $ManifestOnly) { $upload = @($Files) + $upload }
gh release upload $Tag @upload --repo $Repo --clobber
if ($LASTEXITCODE -ne 0) { throw "gh release upload failed" }
Write-Host "Published $Tag"
