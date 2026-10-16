param(
    [switch]$UnitOnly,
    [string]$BuildDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'outputs\build'),
    [string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) ('work\tests-' + [Guid]::NewGuid().ToString('N')))
)
$ErrorActionPreference = 'Stop'
$exe = Join-Path ([IO.Path]::GetFullPath($BuildDirectory)) 'ChromeDevToolsAutoAllow.Tests.exe'
if (!(Test-Path -LiteralPath $exe)) { & (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $BuildDirectory }
$full = [IO.Path]::GetFullPath($OutputDirectory)
if ($full.Contains('"')) { throw 'Invalid output path.' }
if (Test-Path -LiteralPath (Join-Path $full 'self-test.log')) { throw 'Use a fresh test output directory.' }
New-Item -ItemType Directory -Path $full -Force | Out-Null
$mode = if ($UnitOnly) { '--unit-test' } else { '--self-test' }
if (!$UnitOnly) { Write-Host 'Interactive local fixtures may appear. No real Chrome permission is clicked.' }
$p = Start-Process -FilePath $exe -ArgumentList @($mode, ('"' + $full + '"')) -WindowStyle Hidden -PassThru
if (!$p.WaitForExit(90000)) { Stop-Process -Id $p.Id; throw 'Test run exceeded 90 seconds.' }
$code = $p.ExitCode
Get-Content -LiteralPath (Join-Path $full 'self-test.log')
if ($code -ne 0) { throw "Tests failed (exit $code). Logs: $full" }
Write-Host "All tests passed. Logs: $full"
