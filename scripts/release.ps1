param(
    [string]$BuildDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'outputs\build'),
    [string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'outputs'),
    [switch]$SkipBuild,
    [switch]$IncludeSource
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$version = (Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid VERSION.' }
if (!$SkipBuild) {
    & (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $BuildDirectory
    & (Join-Path $PSScriptRoot 'test.ps1') -UnitOnly -BuildDirectory $BuildDirectory
}
$exe = Join-Path ([IO.Path]::GetFullPath($BuildDirectory)) 'ChromeDevToolsAutoAllow.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'Build the application first.' }
if ((Get-Item -LiteralPath $exe).VersionInfo.FileVersion -ne "$version.0") { throw 'Built EXE version does not match VERSION.' }
$out = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $out -Force | Out-Null
$name = "ChromeDevToolsAutoAllow-v$version-windows-x64"
$stage = Join-Path $out $name
$zip = Join-Path $out ($name + '.zip')
$sourceZip = Join-Path $out "ChromeDevToolsAutoAllow-v$version-source.zip"
if ((Test-Path -LiteralPath $stage) -or (Test-Path -LiteralPath $zip)) { throw 'Release destination exists; use a fresh directory. Existing releases are not overwritten.' }
if ($IncludeSource) {
    if (Test-Path -LiteralPath $sourceZip) { throw 'Source archive already exists.' }
    git -C $root rev-parse --verify HEAD | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Source packaging requires a Git commit.' }
    git -C $root diff --quiet HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Commit tracked changes before packaging source.' }
    $untracked = @(git -C $root ls-files --others --exclude-standard)
    if ($untracked.Count -ne 0) { throw 'Untracked source files exist; commit or ignore them before packaging.' }
}
New-Item -ItemType Directory -Path $stage | Out-Null
Copy-Item -LiteralPath $exe -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'LICENSE'),(Join-Path $root 'VERSION') -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'docs\USER_GUIDE.md') -Destination (Join-Path $stage 'README.md')
Copy-Item -LiteralPath (Join-Path $root "docs\releases\v$version.md") -Destination (Join-Path $stage 'RELEASE_NOTES.md')
Compress-Archive -LiteralPath $stage -DestinationPath $zip
$manifest = Join-Path $out "ChromeDevToolsAutoAllow-v$version-SHA256SUMS.txt"
$sum = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
$sums = "$sum  $name.zip`n"
if ($IncludeSource) {
    git -C $root archive --format=zip ("--prefix=ChromeDevToolsAutoAllow-v$version/") ("--output=$sourceZip") HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Git source archive failed.' }
    $sourceSum = (Get-FileHash -LiteralPath $sourceZip -Algorithm SHA256).Hash.ToLowerInvariant()
    $sums += "$sourceSum  ChromeDevToolsAutoAllow-v$version-source.zip`n"
}
[IO.File]::WriteAllText($manifest, $sums, (New-Object Text.UTF8Encoding($false)))
Write-Host "Release package: $zip"
Write-Host "Checksums: $manifest"
