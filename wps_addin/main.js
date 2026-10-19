// Prueba de lectura: no modifica el documento ni copia su contenido fuera de WPS.
function OnAddInLoad() { return true; }

function SpanishWriterProbe() {
    try {
        var app = wps.Application;
        var selection = app.Selection;
        var start = selection.Start;
        var end = selection.End;
        if (start !== end) {
            alert("SpanishWriter: pon el cursor al final de «prueva» sin seleccionar texto.");
            return;
        }
        var from = Math.max(0, start - 80);
        var before = app.ActiveDocument.Range(from, start).Text;
        var match = before.match(/([A-Za-zÁÉÍÓÚÜÑáéíóúüñ]+)$/);
        if (!match) {
            alert("SpanishWriter: no encontré una palabra inmediatamente antes del cursor.");
            return;
        }
        alert("SpanishWriter puede leer la palabra: «" + match[1] + "». El documento no se modificó.");
    } catch (error) {
        alert("SpanishWriter no pudo leer la selección de WPS: " + String(error));
    }
}
