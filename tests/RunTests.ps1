$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$framework = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$output = Join-Path $project 'obj\tests'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$references = @(
    (Join-Path $framework 'System.Web.Extensions.dll'),
    (Join-Path $framework 'WPF\WindowsBase.dll'),
    (Join-Path $framework 'WPF\UIAutomationClient.dll'),
    (Join-Path $framework 'WPF\UIAutomationTypes.dll')
) | ForEach-Object { "/reference:$_" }
$dictionaries = Get-ChildItem (Join-Path $project 'dictionaries') -Filter '*.txt' | ForEach-Object { "/resource:$($_.FullName),SpanishWriter.Dict.$($_.Name)" }
$cases = @(
    @{ Name = 'ReplacementGuardTest'; Sources = @('src\ReplacementGuard.cs') },
    @{ Name = 'AccentRulesTest'; Sources = @('src\AccentRules.cs') },
    @{ Name = 'SpellApiTest'; Sources = @('src\SpellApi.cs', 'src\HunspellFallback.cs', 'src\CustomDictionary.cs') },
    @{ Name = 'CustomDictionaryTest'; Sources = @('src\SpellApi.cs', 'src\HunspellFallback.cs', 'src\CustomDictionary.cs') },
    @{ Name = 'HarperBridgeTest'; Sources = @('src\HarperBridge.cs') }
)
foreach ($case in $cases) {
    $executable = Join-Path $output ($case.Name + '.exe')
    $sources = @((Join-Path $PSScriptRoot ($case.Name + '.cs')))
    $sources += $case.Sources | ForEach-Object { Join-Path $project $_ }
    & $compiler /nologo /codepage:65001 /target:exe /platform:x64 "/out:$executable" @references @dictionaries @sources
    if ($LASTEXITCODE -ne 0) { throw "Error al compilar $($case.Name)." }
    & $executable
    if ($LASTEXITCODE -ne 0) { throw "Falló $($case.Name)." }
}
