using Recode.Core.Native;

namespace Recode.Tests.Fakes;

/// <summary>
/// A loader that never finds anything.
/// </summary>
/// <remarks>
/// Stands in for a broken installation. Every code path that reaches a bundled
/// library has to survive this and produce a sentence a person can act on,
/// rather than an exception from somewhere inside the interop layer.
/// </remarks>
public sealed class MissingNativeLibraryLoader : INativeLibraryLoader
{
    public MissingNativeLibraryLoader(string searchDirectory = @"C:\Program Files\Recode")
    {
        SearchDirectory = searchDirectory;
    }

    public string SearchDirectory { get; }

    public List<string> Requested { get; } = new();

    public bool TryLoad(string fileName, out INativeLibrary? library, out string? error)
    {
        Requested.Add(fileName);
        library = null;
        error = $"{fileName} was not found in {SearchDirectory}.";
        return false;
    }
}

/// <summary>
/// A loader whose library loads but exports nothing.
/// </summary>
/// <remarks>
/// The other way a bundled library goes wrong: the file is present but is a
/// different version or a different build than the one expected. That has to
/// produce a readable message too, not an access violation on the first call
/// through a null function pointer.
/// </remarks>
public sealed class EmptyNativeLibraryLoader : INativeLibraryLoader
{
    public string SearchDirectory => @"C:\Program Files\Recode";

    public bool TryLoad(string fileName, out INativeLibrary? library, out string? error)
    {
        library = new EmptyLibrary(fileName, Path.Combine(SearchDirectory, fileName));
        error = null;
        return true;
    }

    private sealed class EmptyLibrary : INativeLibrary
    {
        public EmptyLibrary(string name, string path)
        {
            Name = name;
            Path = path;
        }

        public string Name { get; }

        public string? Path { get; }

        public bool TryGetExport(string name, out nint address)
        {
            address = 0;
            return false;
        }

        public nint GetExport(string name) =>
            throw new NativeLibraryException(
                $"{Name} does not export '{name}'. The file is probably a different build or version than the one Recode expects.");

        public void Dispose() { }
    }
}
