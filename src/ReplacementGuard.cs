using System;
using System.Threading;

// UI Automation can return from Select before the editor updates its selection.
// Never send replacement text until the editor confirms the exact target range.
internal static class ReplacementGuard
{
    internal static bool TryReplace(Action focus, Func<bool> hasFocus, Func<bool> wordMatches,
        Action select, Func<bool> selectionMatches, Action type, Action wait = null)
    {
        if (wait == null) wait = () => Thread.Sleep(20);
        focus();
        int attempt = 0;
        while (!hasFocus())
        {
            if (++attempt >= 15) return false;
            wait();
        }
        if (!wordMatches()) return false;
        select();
        int confirmations = 0;
        for (attempt = 0; attempt < 15; attempt++)
        {
            if (!hasFocus() || !wordMatches()) return false;
            confirmations = selectionMatches() ? confirmations + 1 : 0;
            if (confirmations == 2)
            {
                // Recheck focus after querying the provider, immediately before input.
                if (!hasFocus()) return false;
                type();
                return true;
            }
            wait();
        }
        return false;
    }
}
