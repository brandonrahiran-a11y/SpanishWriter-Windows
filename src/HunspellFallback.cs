using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

internal sealed class HunspellFallback : IDisposable
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr LoadLibrary(string fileName);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern IntPtr GetProcAddress(IntPtr module, string name);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool FreeLibrary(IntPtr module);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Create(byte[] aff, byte[] dic);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Spell(IntPtr handle, byte[] word);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Suggest(IntPtr handle, out IntPtr list, byte[] word);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void FreeList(IntPtr handle, ref IntPtr list, int count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Destroy(IntPtr handle);

    readonly IntPtr module, handle;
    readonly Spell spell;
    readonly Suggest suggest;
    readonly FreeList freeList;
    readonly Destroy destroy;

    static byte[] Utf8(string text) { return Encoding.UTF8.GetBytes(text + "\0"); }

    static string ReadUtf8(IntPtr pointer)
    {
        if (pointer == IntPtr.Zero) return "";
        int count = 0;
        while (Marshal.ReadByte(pointer, count) != 0 && count < 512) count++;
        var bytes = new byte[count];
        Marshal.Copy(pointer, bytes, 0, count);
        return Encoding.UTF8.GetString(bytes);
    }

    HunspellFallback(string dll, string aff, string dic)
    {
        module = LoadLibrary(dll);
        if (module == IntPtr.Zero) throw new InvalidOperationException("No se pudo cargar Hunspell.");
        var create = Function<Create>("Hunspell_create");
        spell = Function<Spell>("Hunspell_spell");
        suggest = Function<Suggest>("Hunspell_suggest");
        freeList = Function<FreeList>("Hunspell_free_list");
        destroy = Function<Destroy>("Hunspell_destroy");
        handle = create(Utf8(aff), Utf8(dic));
        if (handle == IntPtr.Zero) throw new InvalidOperationException("No se pudo abrir el diccionario español.");
    }

    T Function<T>(string name) where T : class
    {
        IntPtr address = GetProcAddress(module, name);
        if (address == IntPtr.Zero) throw new MissingMethodException(name);
        return (T)(object)Marshal.GetDelegateForFunctionPointer(address, typeof(T));
    }

    public static HunspellFallback TryCreate()
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kingsoft", "WPS Office");
        if (!Directory.Exists(root)) return null;
        foreach (string version in Directory.GetDirectories(root).OrderByDescending(x => x))
        {
            string office = Path.Combine(version, "office6");
            string dictionary = Path.Combine(office, "dicts", "spellcheck", "es_CO");
            string dll = Path.Combine(office, "libhunspell.dll");
            string aff = Path.Combine(dictionary, "main.aff"), dic = Path.Combine(dictionary, "main.dic");
            if (!File.Exists(dll) || !File.Exists(aff) || !File.Exists(dic)) continue;
            try { return new HunspellFallback(dll, aff, dic); } catch { }
        }
        return null;
    }

    public bool IsCorrect(string word) { return spell(handle, Utf8(word)) != 0; }

    public string[] Suggestions(string word)
    {
        IntPtr list;
        int count = suggest(handle, out list, Utf8(word));
        if (count <= 0 || list == IntPtr.Zero) return new string[0];
        try
        {
            return Enumerable.Range(0, Math.Min(count, 3))
                .Select(i => ReadUtf8(Marshal.ReadIntPtr(list, i * IntPtr.Size)))
                .Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        }
        finally { freeList(handle, ref list, count); }
    }

    public void Dispose()
    {
        if (handle != IntPtr.Zero) destroy(handle);
        if (module != IntPtr.Zero) FreeLibrary(module);
    }
}
