param([string]$WorkDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'work\icon-build'))
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Path $WorkDirectory -Force | Out-Null
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$generator = Join-Path ([IO.Path]::GetFullPath($WorkDirectory)) 'IconArtwork.exe'
& $csc /nologo /target:exe /platform:x64 /reference:System.Drawing.dll ('/out:' + $generator) (Join-Path $root 'tools\IconArtwork.cs')
if ($LASTEXITCODE -ne 0) { throw 'Icon artwork compilation failed.' }
& $generator (Join-Path $root 'assets')
if ($LASTEXITCODE -ne 0) { throw 'Icon artwork generation failed.' }
