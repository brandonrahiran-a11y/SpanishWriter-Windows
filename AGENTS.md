# SpanishWriter: instrucciones para agentes (Claude Code y Codex)

Corrector ortográfico en español para Windows. Observa el campo de texto activo mediante UI Automation y muestra sugerencias junto a la palabra.

## Lenguaje y compilación

- C# 5 compilado con `csc.exe` de .NET Framework 4.x (`C:\Windows\Microsoft.NET\Framework64\v4.0.30319`). **No uses C# 6 o posterior**: nada de interpolación `$""`, `?.`, miembros con `=>`, `nameof`, `out var` ni inicializadores de propiedades automáticas.
- Para compilar: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1`. Genera `SpanishWriter.exe` e incluye `dictionaries/*.txt` como recursos.
- Para las pruebas: `powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\RunTests.ps1`. Cada prueba compila solo los archivos de `src` que necesita; las que usan `Issue` la definen en el propio archivo de prueba.
- Si el ejecutable está abierto, ciérralo antes de compilar.

## Estructura

- `src/SpellApi.cs`: motor (`WindowsSpeller`). Usa el corrector de Windows por COM; si no está, Hunspell de WPS; si tampoco, reglas locales. Contiene las variantes con tilde, las mayúsculas y el límite `MaxIssues`.
- `src/CustomDictionary.cs`: diccionario complementario incluido en el ejecutable, diccionario personal (`%APPDATA%\SpanishWriter\diccionario-personal.txt`) y sugerencias cercanas.
- `src/AccentRules.cs`: tildes por contexto para palabras que existen pero llevan tilde según la frase.
- `src/SpanishWriter.cs`: interfaz (`Theme`, `Chip`, marcas, panel `Writer`), UI Automation y reemplazo.
- `src/ReplacementGuard.cs`: confirma la selección antes de escribir la corrección.
- `src/HarperBridge.cs`: modo inglés opcional con `harper-cli.exe` (en `engines/` o en el PATH).
- `src/WpsAddinServer.cs` y `wps_addin/`: complemento experimental de WPS Writer que pide sugerencias a `127.0.0.1:17825`.
- `dictionaries/medico.txt`, `dictionaries/general.txt`: una palabra por línea; `#` marca un comentario.

## Reglas

- En las reglas de tildes, un falso positivo es peor que un error no detectado. Cada regla nueva lleva casos de prueba negativos.
- Los subrayados y las etiquetas usan un solo azul, `Theme.Mark` (0, 170, 224). Los botones usan `Theme.Accent` (0, 112, 168), un azul más oscuro. No agregues otros colores: el usuario pidió expresamente un solo color para las marcas.
- No escribas datos de pacientes ni textos personales en pruebas, registros, incidencias ni diccionarios.
- El sandbox de Codex no tiene el corrector COM de Windows ni UI Automation, y no puede abrir la app. Las pruebas que dependen de Windows se comprueban en la máquina real.
