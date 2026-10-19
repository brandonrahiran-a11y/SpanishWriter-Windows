# SpanishWriter para Windows

Corrector local que observa el campo de texto activo y muestra sugerencias junto a la palabra. Intenta revisar cualquier aplicación que exponga su texto y posición mediante la API de accesibilidad de Windows. Se ha probado en campos de Edge, Opera y DuckDuckGo. La compatibilidad con CMD/Windows Terminal y documentos de WPS/Office depende de la información que cada programa expone a Windows.

## Corrección

- Primero intenta usar el corrector de español de Windows.
- Si Windows no permite abrirlo, intenta usar el diccionario `es_CO` de Hunspell instalado con WPS Office. Solo usa los archivos ya instalados; este repositorio no incluye WPS, Hunspell ni sus diccionarios.
- Si ninguno está disponible, aplica una lista breve de errores frecuentes y reglas contextuales de tildes.
- El texto se procesa localmente y no se envía a servidores.

La línea azul bajo la palabra se puede abrir con el puntero o con un clic. En consola, el corrector solo intenta reemplazar una palabra situada inmediatamente antes del cursor cuando Windows confirma su posición exacta. Si no puede comprobarla, copia la sugerencia al portapapeles sin modificar el comando. No ejecuta comandos.

## Compilar

Requiere Windows con .NET Framework 4.x. En PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1
```

Abre `SpanishWriter.exe`. Una tarjeta de estado confirma que se inició y se cierra sola tras seis segundos. El icono azul queda junto al reloj o dentro de los iconos ocultos (flecha ^). Su menú permite pausar o salir. Si vuelves a abrir el ejecutable, se muestra la tarjeta de la instancia activa.

El icono de la aplicación procede de `assets/logo.png`. `CreateIcon.ps1` genera los tamaños de Windows en `assets/SpanishWriter.ico`; `Build.ps1` lo incorpora al ejecutable. Si cambias el logo, ejecuta primero `CreateIcon.ps1` y después `Build.ps1`.

## Pruebas

`tests/AccentRulesTest.cs` comprueba reglas contextuales de tildes. `tests/SpellApiTest.cs` comprueba las reglas locales y, si WPS está instalado, la lectura de su diccionario. Las pruebas no requieren datos personales.

## Limitaciones

Windows Terminal puede no exponer la posición exacta del cursor. La aplicación omite el reemplazo cuando no puede ubicar la palabra con seguridad.

En la versión de WPS probada el 18 de octubre de 2026, el editor no expone el texto del documento a la API de accesibilidad de Windows, ni en la vista normal ni en la vista completa. SpanishWriter no puede subrayar ni reemplazar palabras dentro de ese editor. Para corregir allí, activa el corrector integrado de WPS: **Revisar > Corrector ortográfico > Establecer idioma de corrección > Español**. WPS permite mostrar subrayados mientras escribes.

Hay un prototipo de complemento de WPS Writer en `wps_addin/`. SpanishWriter sirve sus archivos y sugerencias solo en `127.0.0.1:17825`; `wps_addin/Install-WpsProbe.ps1` registra esa dirección en el perfil de WPS. El complemento admite leer una palabra seleccionada o la palabra anterior al cursor y ofrece un botón para corregir únicamente ese rango si WPS confirma que no cambió. El subrayado automático dentro de WPS todavía está en desarrollo. WPS debe reiniciarse completamente después de instalarlo.

Harper es un corrector de gramática para inglés. No se aplica al texto español.
Harper está disponible como modo inglés opcional en el menú del icono. Descarga el archivo oficial `harper-cli-x86_64-pc-windows-msvc.zip` de las [versiones de Harper](https://github.com/Automattic/harper/releases), extrae `harper-cli.exe` en `engines/` junto al ejecutable y reinicia SpanishWriter. También se reconoce si `harper-cli.exe` está en `PATH`. Harper no está incluido en este repositorio.

## Privacidad y contribuciones

No publiques registros, capturas con datos privados ni textos de pacientes en incidencias o solicitudes de cambio. Se aceptan mejoras en detección de campos, compatibilidad con consolas, diccionarios, accesibilidad y pruebas.
