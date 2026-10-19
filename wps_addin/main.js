// WPS reads and edits only the exact word selected in its document.
function OnAddInLoad() { return true; }

function SpanishWriterWord() {
    var app = wps.Application;
    var selection = app.Selection;
    var start = selection.Start;
    var end = selection.End;
    var word;
    if (start !== end) {
        word = selection.Range.Text;
        if (!/^[A-Za-zÁÉÍÓÚÜÑáéíóúüñ]+$/.test(word)) throw new Error("Selecciona solo una palabra.");
    } else {
        var from = Math.max(0, start - 80);
        var before = app.ActiveDocument.Range(from, start).Text;
        var match = before.match(/([A-Za-zÁÉÍÓÚÜÑáéíóúüñ]+)$/);
        if (!match) throw new Error("Pon el cursor al final de una palabra o selecciónala.");
        word = match[1];
        start -= word.length;
    }
    if (end - start !== word.length) throw new Error("La posición de la palabra no coincide.");
    return { app: app, selection: selection, document: app.ActiveDocument, start: start, end: end, word: word };
}

function SpanishWriterProbe() {
    try {
        var item = SpanishWriterWord();
        alert("SpanishWriter puede leer la palabra: «" + item.word + "». El documento no se modificó.");
    } catch (error) {
        alert("SpanishWriter: " + String(error));
    }
}

function SpanishWriterCorrect() {
    var item;
    try { item = SpanishWriterWord(); }
    catch (error) { alert("SpanishWriter: " + String(error)); return; }
    var request = new XMLHttpRequest();
    request.onreadystatechange = function () {
        if (request.readyState !== 4) return;
        try {
            if (request.status !== 200) throw new Error("El motor local no respondió.");
            var response = JSON.parse(request.responseText);
            var replacement = response.suggestions && response.suggestions[0];
            if (!replacement || replacement === item.word) {
                alert("SpanishWriter no encontró una corrección para «" + item.word + "».");
                return;
            }
            if (item.selection.Start !== item.start && item.selection.Start !== item.end)
                throw new Error("El cursor cambió; vuelve a seleccionar la palabra.");
            var range = item.document.Range(item.start, item.end);
            if (range.Text !== item.word) throw new Error("El documento cambió; no se aplicó la corrección.");
            range.Text = replacement;
            if (item.document.Range(item.start, item.start + replacement.length).Text !== replacement)
                throw new Error("WPS no confirmó el reemplazo.");
            item.selection.SetRange(item.start + replacement.length, item.start + replacement.length);
        } catch (error) {
            alert("SpanishWriter: " + String(error));
        }
    };
    request.open("GET", "http://127.0.0.1:17825/wps/suggest?word=" + encodeURIComponent(item.word), true);
    request.send();
}
