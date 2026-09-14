param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$icon=Join-Path $PSScriptRoot 'assets\ahu.ico'
if(!(Test-Path -LiteralPath $icon)) {& (Join-Path $PSScriptRoot 'make-icon.ps1')}
$references = @('/r:System.dll','/r:System.Core.dll','/r:System.Web.dll','/r:System.Web.Extensions.dll','/r:System.Net.Http.dll','/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Security.dll')
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 "/win32manifest:$PSScriptRoot/app.manifest" "/win32icon:$icon" "/out:$OutputDirectory/AHU校园网助手.exe" @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
