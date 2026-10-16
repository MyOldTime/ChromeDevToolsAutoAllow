param(
    [string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'outputs\build'),
    [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $framework 'csc.exe'
if (!(Test-Path -LiteralPath $csc)) { throw 'Windows x64 .NET Framework C# compiler was not found.' }
$out = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $out -Force | Out-Null
$version = (Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid VERSION.' }
$assemblyInfo = Join-Path $root 'src\ChromeDevToolsAutoAllow\Properties\AssemblyInfo.cs'
if ((Get-Content -LiteralPath $assemblyInfo -Raw) -notmatch ('AssemblyVersion\("' + [regex]::Escape($version) + '\.0"\)')) { throw 'VERSION and AssemblyInfo.cs disagree.' }
$appIcon = Join-Path $root 'assets\app.ico'
$trayIcon = Join-Path $root 'assets\tray.ico'
if (!(Test-Path -LiteralPath $appIcon) -or !(Test-Path -LiteralPath $trayIcon)) { & (Join-Path $PSScriptRoot 'build-icons.ps1') }
$references = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll',
    (Join-Path $framework 'WPF\UIAutomationClient.dll'),
    (Join-Path $framework 'WPF\UIAutomationTypes.dll'),
    (Join-Path $framework 'WPF\WindowsBase.dll'))
$common = @('/nologo','/platform:x64','/optimize+','/warn:4','/warnaserror+')
foreach ($reference in $references) { $common += '/reference:' + $reference }
$exe = Join-Path $out 'ChromeDevToolsAutoAllow.exe'
$appArgs = $common + @('/target:winexe', ('/out:' + $exe),
    ('/win32manifest:' + (Join-Path $root 'src\ChromeDevToolsAutoAllow\app.manifest')),
    ('/win32icon:' + $appIcon),
    ('/resource:' + $appIcon + ',ChromeDevToolsAutoAllow.AppIcon'),
    ('/resource:' + $trayIcon + ',ChromeDevToolsAutoAllow.TrayIcon'),
    (Join-Path $root 'src\ChromeDevToolsAutoAllow\Program.cs'), $assemblyInfo)
& $csc @appArgs
if ($LASTEXITCODE -ne 0) { throw "Application compilation failed: $LASTEXITCODE" }
if (!$SkipTests) {
    $testArgs = $common + @('/target:exe', ('/out:' + (Join-Path $out 'ChromeDevToolsAutoAllow.Tests.exe')),
        ('/reference:' + $exe),
        (Join-Path $root 'tests\ChromeDevToolsAutoAllow.Tests\Program.cs'),
        (Join-Path $root 'tests\ChromeDevToolsAutoAllow.Tests\Tests.cs'))
    & $csc @testArgs
    if ($LASTEXITCODE -ne 0) { throw "Test compilation failed: $LASTEXITCODE" }
}
Write-Host "Built ChromeDevToolsAutoAllow $version -> $out"
