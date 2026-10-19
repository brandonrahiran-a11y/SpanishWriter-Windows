$ErrorActionPreference = 'Stop'
$addin = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$required = @('index.html','main.js','ribbon.xml')
foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $addin $name))) { throw "Falta $name" }
}
$configDir = Join-Path $env:APPDATA 'kingsoft\wps\jsaddons'
New-Item -ItemType Directory -Path $configDir -Force | Out-Null
$publish = Join-Path $configDir 'publish.xml'
if (Test-Path -LiteralPath $publish) {
    $backup = "$publish.SpanishWriter-backup"
    if (-not (Test-Path -LiteralPath $backup)) { Copy-Item -LiteralPath $publish -Destination $backup }
    [xml]$xml = Get-Content -LiteralPath $publish -Raw
} else {
    [xml]$xml = '<jsplugins/>'
}
if ($xml.DocumentElement.Name -ne 'jsplugins') { throw 'publish.xml de WPS tiene un formato inesperado.' }
$old = @($xml.DocumentElement.SelectNodes("jspluginonline[@name='SpanishWriterWps']"))
foreach ($item in $old) { [void]$xml.DocumentElement.RemoveChild($item) }
$url = ([System.Uri]::new($addin.TrimEnd('\') + '\')).AbsoluteUri
$entry = $xml.CreateElement('jspluginonline')
$entry.SetAttribute('name','SpanishWriterWps')
$entry.SetAttribute('type','wps')
$entry.SetAttribute('url',$url)
$entry.SetAttribute('enable','true')
[void]$xml.DocumentElement.AppendChild($entry)
$xml.Save($publish)
Write-Output "Complemento registrado en $publish"
Write-Output "URL local: $url"
Write-Output 'Cierra WPS después de guardar tus documentos y vuelve a abrirlo para cargar el complemento.'
