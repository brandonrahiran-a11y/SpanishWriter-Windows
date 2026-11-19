using System;

internal static class ReplacementGuardTest
{
    sealed class Editor
    {
        internal string Text = "una prueva y prueva";
        internal bool Focused = true;
        internal int Start = 19, Length, Writes, Waits;
        internal Action OnSelect, OnWait, OnFocus;

        internal void SelectWord() { Start = 4; Length = 6; }
        internal bool Replace()
        {
            return ReplacementGuard.TryReplace(
                () => { if (OnFocus != null) OnFocus(); },
                () => Focused,
                () => Text == "una prueva y prueva",
                () => { if (OnSelect != null) OnSelect(); },
                () => Start == 4 && Length == 6 && Text.Substring(Start, Length) == "prueva",
                () => {
                    Writes++;
                    Text = Text.Substring(0, Start) + "prueba" + Text.Substring(Start + Length);
                },
                () => { Waits++; if (OnWait != null) OnWait(); });
        }
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void Unchanged(Editor editor, string scenario)
    {
        Assert(!editor.Replace() && editor.Writes == 0 && editor.Text == "una prueva y prueva", scenario);
        Console.WriteLine("Sin escritura: " + scenario);
    }

    static int Main()
    {
        var immediate = new Editor();
        immediate.OnSelect = immediate.SelectWord;
        Assert(immediate.Replace() && immediate.Writes == 1 && immediate.Text == "una prueba y prueva",
            "Debe reemplazar solo la primera aparición, conservando el resto.");

        var delayed = new Editor();
        delayed.OnWait = () => { if (delayed.Waits == 3) delayed.SelectWord(); };
        Assert(delayed.Replace() && delayed.Writes == 1 && delayed.Text == "una prueba y prueva" && delayed.Waits >= 4,
            "Debe esperar a que el editor confirme la selección tardía.");

        var ignored = new Editor();
        Unchanged(ignored, "Select ignorado; el cursor sigue al final");
        Assert(ignored.Waits <= 15, "La espera debe terminar.");

        var wrongOccurrence = new Editor();
        wrongOccurrence.OnSelect = () => { wrongOccurrence.Start = 13; wrongOccurrence.Length = 6; };
        Unchanged(wrongOccurrence, "misma palabra seleccionada en otra posición");

        var collapsed = new Editor();
        collapsed.OnSelect = collapsed.SelectWord;
        collapsed.OnWait = () => { collapsed.Length = 0; };
        Unchanged(collapsed, "selección que desaparece después de Select");

        var lostFocus = new Editor();
        lostFocus.OnSelect = lostFocus.SelectWord;
        lostFocus.OnWait = () => { lostFocus.Focused = false; };
        Unchanged(lostFocus, "cambio de foco mientras se confirma la selección");

        var noFocus = new Editor { Focused = false };
        noFocus.OnSelect = () => { throw new Exception("No debe seleccionar sin foco."); };
        Unchanged(noFocus, "activación del campo fallida");

        var delayedFocus = new Editor { Focused = false };
        delayedFocus.OnWait = () => { if (delayedFocus.Waits == 2) delayedFocus.Focused = true; };
        delayedFocus.OnSelect = delayedFocus.SelectWord;
        Assert(delayedFocus.Replace() && delayedFocus.Text == "una prueba y prueva", "Debe esperar el foco.");

        var stale = new Editor { Text = "texto editado por el usuario" };
        stale.OnSelect = () => { throw new Exception("No debe seleccionar un rango obsoleto."); };
        Assert(!stale.Replace() && stale.Writes == 0, "No debe corregir texto cambiado.");

        var changedDuringSelection = new Editor();
        changedDuringSelection.OnSelect = changedDuringSelection.SelectWord;
        changedDuringSelection.OnWait = () => { changedDuringSelection.Text = "texto nuevo"; };
        Assert(!changedDuringSelection.Replace() && changedDuringSelection.Writes == 0 &&
            changedDuringSelection.Text == "texto nuevo", "Debe conservar las ediciones durante la selección.");

        bool propagated = false;
        try
        {
            ReplacementGuard.TryReplace(() => { }, () => true, () => true, () => { }, () => true,
                () => { throw new InvalidOperationException("entrada parcial"); }, () => { });
        }
        catch (InvalidOperationException) { propagated = true; }
        Assert(propagated, "Un fallo de escritura debe propagarse, sin intentar otro reemplazo.");
        Console.WriteLine("11 escenarios de reemplazo correctos.");
        return 0;
    }
}
