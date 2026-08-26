using Recode.Core.Abstractions;

namespace Recode.Core.Conversion;

/// <summary>
/// Where a converted file lands.
/// </summary>
/// <param name="Path">The full path to write to.</param>
/// <param name="OverwritesExisting">A file is already there and will be replaced.</param>
/// <param name="SameAsInput">The output path is the input path.</param>
public sealed record OutputPath(string Path, bool OverwritesExisting, bool SameAsInput);

/// <summary>
/// Works out the output file name, adding "(1)", "(2)" and so on rather than
/// overwriting anything by accident.
/// </summary>
/// <remarks>
/// The suffix style matches what Explorer does when you copy a file into a
/// folder that already has one by that name, so the result looks like something
/// Windows did rather than something this program invented.
///
/// Passing force means the caller has accepted an overwrite, which is also how
/// recompressing to the same format in place is expressed.
/// </remarks>
public sealed class OutputPathResolver
{
    /// <summary>
    /// A ceiling on the suffix search. Reaching it means something is wrong
    /// with the folder, not that the user needs a ten thousandth copy.
    /// </summary>
    public const int MaxAttempts = 9999;

    private readonly IFileSystem _fileSystem;

    public OutputPathResolver(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    /// <summary>
    /// Resolves the destination for one conversion.
    /// </summary>
    /// <param name="inputPath">The file being converted.</param>
    /// <param name="targetExtension">Extension with a leading dot, for example ".png".</param>
    /// <param name="outputDirectory">Where to write, or null to write beside the input.</param>
    /// <param name="force">Allow an existing file to be replaced instead of finding a free name.</param>
    public OutputPath Resolve(string inputPath, string targetExtension, string? outputDirectory, bool force)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetExtension);

        if (!targetExtension.StartsWith('.'))
        {
            targetExtension = "." + targetExtension;
        }

        var directory = string.IsNullOrWhiteSpace(outputDirectory)
            ? System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(inputPath)) ?? "."
            : outputDirectory;

        var baseName = System.IO.Path.GetFileNameWithoutExtension(inputPath);
        if (string.IsNullOrEmpty(baseName))
        {
            throw new ArgumentException($"'{inputPath}' has no file name to work from.", nameof(inputPath));
        }

        var candidate = System.IO.Path.Combine(directory, baseName + targetExtension);

        if (force)
        {
            return new OutputPath(
                candidate,
                _fileSystem.FileExists(candidate),
                _fileSystem.SamePath(candidate, inputPath));
        }

        if (!_fileSystem.FileExists(candidate))
        {
            return new OutputPath(candidate, false, false);
        }

        // Something is already there. That includes the case where the target
        // format equals the source format, in which case the file in the way is
        // the input itself and a renamed copy is exactly what is wanted.
        for (var suffix = 1; suffix <= MaxAttempts; suffix++)
        {
            var withSuffix = System.IO.Path.Combine(directory, $"{baseName} ({suffix}){targetExtension}");
            if (!_fileSystem.FileExists(withSuffix))
            {
                return new OutputPath(withSuffix, false, false);
            }
        }

        throw new IOException(
            $"Could not find a free name for '{baseName}{targetExtension}' in {directory} after {MaxAttempts} attempts.");
    }
}
