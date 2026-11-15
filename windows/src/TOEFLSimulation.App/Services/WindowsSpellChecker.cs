using System.Runtime.InteropServices;
using TOEFLSimulation.Core;

namespace TOEFLSimulation.App.Services;

/// <summary>
/// <see cref="ISpellChecker"/> using the Windows Spell Checking API (ISpellCheckerFactory, en-US) through COM interop, in
/// place of the Mac app's NSSpellChecker. Every flagged error counts once, left to right, like the Mac loop; repeated-word
/// warnings are not counted (NSSpellChecker's spelling check does not flag them). If the API or the en-US dictionary is
/// unavailable, nothing is counted (the Writing rubric then behaves like Core's <see cref="NoSpellChecker"/>).
/// </summary>
public sealed class WindowsSpellChecker : ISpellChecker
{
    private const string Language = "en-US";

    public int CountMisspellings(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        object? factoryObject = null;
        ISpellCheckerNative? checker = null;
        IEnumSpellingError? errors = null;
        try
        {
            // A new checker per call keeps the COM objects on the calling thread's apartment.
            var type = Type.GetTypeFromCLSID(new Guid("7AB36653-1796-484B-BDFA-E74F1DB7C1DC"), throwOnError: false);
            if (type is null) return 0;
            factoryObject = Activator.CreateInstance(type);
            if (factoryObject is not ISpellCheckerFactory factory) return 0;
            if (factory.IsSupported(Language) == 0) return 0;
            checker = factory.CreateSpellChecker(Language);
            errors = checker.Check(text);
            var count = 0;
            while (errors.Next(out var error) == 0 && error is not null)
            {
                try
                {
                    if (error.CorrectiveAction != CorrectiveAction.Delete) count++;
                }
                finally
                {
                    Marshal.ReleaseComObject(error);
                }
            }
            return count;
        }
        catch (Exception error)
        {
            CrashLog.Write(error);
            return 0;
        }
        finally
        {
            if (errors is not null) Marshal.ReleaseComObject(errors);
            if (checker is not null) Marshal.ReleaseComObject(checker);
            if (factoryObject is not null && Marshal.IsComObject(factoryObject)) Marshal.ReleaseComObject(factoryObject);
        }
    }

    private enum CorrectiveAction
    {
        None = 0,
        GetSuggestions = 1,
        Replace = 2,
        Delete = 3,
    }

    [ComImport, Guid("8E018A9D-2415-4677-BF08-794EA61F94BB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISpellCheckerFactory
    {
        [return: MarshalAs(UnmanagedType.Interface)]
        object GetSupportedLanguages();

        int IsSupported([MarshalAs(UnmanagedType.LPWStr)] string languageTag);

        [return: MarshalAs(UnmanagedType.Interface)]
        ISpellCheckerNative CreateSpellChecker([MarshalAs(UnmanagedType.LPWStr)] string languageTag);
    }

    [ComImport, Guid("B6FD0B71-E2BC-4653-8D05-F197E412770B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISpellCheckerNative
    {
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetLanguageTag();

        [return: MarshalAs(UnmanagedType.Interface)]
        IEnumSpellingError Check([MarshalAs(UnmanagedType.LPWStr)] string text);
    }

    [ComImport, Guid("803E3BD4-2828-4410-8290-418D1D73C762"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumSpellingError
    {
        [PreserveSig]
        int Next([MarshalAs(UnmanagedType.Interface)] out ISpellingError? value);
    }

    [ComImport, Guid("B7C82D61-FBE8-4B47-9B27-6C0D2E0DE0A3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISpellingError
    {
        uint StartIndex { get; }
        uint Length { get; }
        CorrectiveAction CorrectiveAction { get; }
    }
}
