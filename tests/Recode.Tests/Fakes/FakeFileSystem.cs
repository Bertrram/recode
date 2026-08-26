using Recode.Core.Abstractions;

namespace Recode.Tests.Fakes;

/// <summary>
/// An in memory file system, so output naming can be tested without touching a disk.
/// </summary>
/// <remarks>
/// Paths are compared the way Windows compares them, case insensitively, which
/// is the behaviour the naming rules have to get right.
/// </remarks>
public sealed class FakeFileSystem : IFileSystem
{
    private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    public FakeFileSystem(params string[] existingFiles)
    {
        foreach (var file in existingFiles)
        {
            AddFile(file);
        }
    }

    public List<(string Source, string Destination, bool Overwrite)> Moves { get; } = new();

    public List<string> Deletes { get; } = new();

    public void AddFile(string path)
    {
        _files.Add(Normalise(path));

        var directory = Path.GetDirectoryName(Normalise(path));
        if (!string.IsNullOrEmpty(directory))
        {
            _directories.Add(directory);
        }
    }

    public bool FileExists(string path) => _files.Contains(Normalise(path));

    public bool DirectoryExists(string path) => _directories.Contains(Normalise(path));

    public void CreateDirectory(string path) => _directories.Add(Normalise(path));

    public void MoveFile(string source, string destination, bool overwrite)
    {
        Moves.Add((source, destination, overwrite));
        _files.Remove(Normalise(source));
        _files.Add(Normalise(destination));
    }

    public void DeleteFile(string path)
    {
        Deletes.Add(path);
        _files.Remove(Normalise(path));
    }

    public bool SamePath(string first, string second) =>
        string.Equals(Normalise(first), Normalise(second), StringComparison.OrdinalIgnoreCase);

    private static string Normalise(string path) => Path.GetFullPath(path);
}
