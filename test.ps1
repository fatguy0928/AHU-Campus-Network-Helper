$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if(!(Test-Path -LiteralPath $compiler)) {$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'}
$testOut=Join-Path $PSScriptRoot 'test-results'
New-Item -ItemType Directory -Path $testOut -Force | Out-Null
$references=@('/r:System.dll','/r:System.Core.dll','/r:System.Web.dll','/r:System.Web.Extensions.dll','/r:System.Net.Http.dll','/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Security.dll')
$sources=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object {$_.FullName})
$tests=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' | ForEach-Object {$_.FullName})
& $compiler /nologo /target:exe /codepage:65001 /main:ProtocolTests "/out:$testOut/ProtocolTests.exe" @references @sources @tests
if($LASTEXITCODE -ne 0) {throw 'Test compilation failed'}
& (Join-Path $testOut 'ProtocolTests.exe')
if($LASTEXITCODE -ne 0) {throw 'Protocol tests failed'}
& $compiler /nologo /target:exe /codepage:65001 /main:UiTests "/win32manifest:$PSScriptRoot/app.manifest" "/win32icon:$PSScriptRoot/assets/ahu.ico" "/out:$testOut/UiTests.exe" @references @sources @tests
if($LASTEXITCODE -ne 0) {throw 'UI test compilation failed'}
& (Join-Path $testOut 'UiTests.exe') (Join-Path $testOut 'ui-preview.png')
if($LASTEXITCODE -ne 0) {throw 'UI tests failed'}
& $compiler /nologo /target:exe /codepage:65001 /main:TransportTests "/out:$testOut/TransportTests.exe" @references @sources @tests
if($LASTEXITCODE -ne 0) {throw 'Transport test compilation failed'}
& (Join-Path $testOut 'TransportTests.exe')
if($LASTEXITCODE -ne 0) {throw 'Transport tests failed'}
& $compiler /nologo /target:exe /codepage:65001 /main:IconTests "/out:$testOut/IconTests.exe" @references @sources @tests
if($LASTEXITCODE -ne 0) {throw 'Icon test compilation failed'}
& (Join-Path $testOut 'IconTests.exe') (Join-Path $PSScriptRoot 'assets/ahu.ico')
if($LASTEXITCODE -ne 0) {throw 'Icon tests failed'}
