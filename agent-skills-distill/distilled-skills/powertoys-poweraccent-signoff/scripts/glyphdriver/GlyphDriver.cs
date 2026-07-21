// Behavioral glyph driver for the PowerAccent (Quick Accent) sign-off.
//
// Loads the REAL, freshly-built PowerAccent.Common.dll (the single source of truth
// that feeds the overlay's candidate list) and asserts the exact end-user glyph sets
// a user sees when holding a base letter. Unlike the module's own data-invariant
// unit tests, these checks pin SPECIFIC glyphs, so a removed/reordered accent is
// caught. Emits JSON: [{ "id", "ok", "detail" }].
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

class GlyphDriver
{
    static Type _cm = null!, _lang = null!, _letter = null!;
    static MethodInfo _get = null!;

    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Length < 1)
        {
            Console.Error.WriteLine("usage: GlyphDriver <path-to-PowerAccent.Common.dll>");
            return 2;
        }

        var asm = Assembly.LoadFrom(args[0]);
        _cm = asm.GetType("PowerAccent.Common.CharacterMappings")!;
        _lang = asm.GetType("PowerAccent.Common.Language")!;
        _letter = asm.GetType("PowerAccent.Common.LetterKey")!;
        _get = _cm.GetMethod("GetCharacters", BindingFlags.Public | BindingFlags.Static)!;

        var results = new List<object>();

        // P0: French base letters render their canonical accent sets (exact sequence).
        results.Add(Check("glyph-fr-a-exact",
            () => SeqEq(Get("VK_A", "FR"), new[] { "à", "â", "á", "ä", "ã", "æ" })));
        results.Add(Check("glyph-fr-e-exact",
            () => SeqEq(Get("VK_E", "FR"), new[] { "é", "è", "ê", "ë", "€" })));
        results.Add(Check("glyph-fr-c-exact",
            () => SeqEq(Get("VK_C", "FR"), new[] { "ç" })));

        // P1: single-language currency mapping (VK_E -> euro only).
        results.Add(Check("glyph-cur-e-euro",
            () => SeqEq(Get("VK_E", "CUR"), new[] { "€" })));

        // P1: ALL-languages union contains the common Latin 'a' accents (order-independent).
        results.Add(Check("glyph-all-a-contains-common",
            () => Contains(Get("VK_A", "ALL_A"), new[] { "à", "á", "â", "ä", "ã" })));

        Console.WriteLine(JsonSerializer.Serialize(results,
            new JsonSerializerOptions { WriteIndented = false }));
        return 0;
    }

    static string[] Get(string letterName, string langSpec)
    {
        object letter = Enum.Parse(_letter, letterName);
        Array langs;
        if (langSpec == "ALL_A")
        {
            langs = Enum.GetValues(_lang);
        }
        else
        {
            var one = Enum.Parse(_lang, langSpec);
            langs = Array.CreateInstance(_lang, 1);
            langs.SetValue(one, 0);
        }
        var res = _get.Invoke(null, new object[] { letter, langs });
        return ((IEnumerable)res!).Cast<string>().ToArray();
    }

    static object Check(string id, Func<(bool, string)> body)
    {
        try
        {
            var (ok, detail) = body();
            return new { id, ok, detail };
        }
        catch (Exception ex)
        {
            return new { id, ok = false, detail = "EXC: " + (ex.InnerException ?? ex).Message };
        }
    }

    static (bool, string) SeqEq(string[] actual, string[] expected)
    {
        bool ok = actual.SequenceEqual(expected);
        return (ok, $"expected=[{string.Join(",", expected)}] actual=[{string.Join(",", actual)}]");
    }

    static (bool, string) Contains(string[] actual, string[] needles)
    {
        var missing = needles.Where(n => !actual.Contains(n)).ToArray();
        bool ok = missing.Length == 0;
        return (ok, ok ? $"all present in [{string.Join(",", actual)}]"
                       : $"MISSING=[{string.Join(",", missing)}] actual=[{string.Join(",", actual)}]");
    }
}
