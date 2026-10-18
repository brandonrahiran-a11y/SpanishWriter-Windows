$ErrorActionPreference = 'Stop'
$root = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $root 'WPF'
$refs = @('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll') | ForEach-Object { Join-Path $root $_ }
$refs += @('WindowsBase.dll','UIAutomationClient.dll','UIAutomationTypes.dll') | ForEach-Object { Join-Path $wpf $_ }
$exe = Join-Path $PSScriptRoot 'SpanishWriter.exe'
$args = @('/nologo','/codepage:65001','/target:winexe','/platform:x64',"/out:$exe") + ($refs | ForEach-Object { "/reference:$_" }) + (Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | Select-Object -ExpandProperty FullName)
& (Join-Path $root 'csc.exe') $args
if ($LASTEXITCODE -ne 0) { throw 'Error al compilar.' }
Write-Output $exe
