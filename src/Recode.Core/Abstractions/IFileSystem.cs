namespace Recode.Core.Abstractions;

/// <summary>
/// The file system operations the conversion logic performs.
/// </summary>
/// <remarks>
/// Exists so that output naming, including the "(1)" and "(2)" suffixes, can be
/// tested against a dictionary instead of against a temporary directory. That
/// logic has more edge cases than anything else in the project and deserves
/// tests that run in microseconds.
/// </remarks>
public interface IFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    void CreateDirectory(string path);

    /// <summary>Moves a file, replacing the destination when <paramref name="overwrite"/> is set.</summary>
    void MoveFile(string source, string destination, bool overwrite);

    void DeleteFile(string path);

    /// <summary>
    /// The two paths refer to the same file. Compared case insensitively and
    /// with relative segments resolved, because "photo.jpg" and
    /// ".\PHOTO.JPG" are the same file on Windows.
    /// </summary>
    bool SamePath(string first, string second);
}

/// <summary>The real file system.</summary>
public sealed class PhysicalFileSystem : IFileSystem
{
    public static PhysicalFileSystem Instance { get; } = new();

    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public void MoveFile(string source, string destination, bool overwrite) =>
        File.Move(source, destination, overwrite);

    public void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public bool SamePath(string first, string second)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            // An unresolvable path cannot be the same file as anything.
            return false;
        }
    }
}
