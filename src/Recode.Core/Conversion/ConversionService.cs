using Recode.Core.Abstractions;
using Recode.Core.Formats;

namespace Recode.Core.Conversion;

/// <summary>
/// Converts files. The part of the program that does the actual work.
/// </summary>
/// <remarks>
/// Knows nothing about WIC, libheif or libwebp. It asks the registry for a
/// decoder and an encoder, hands RGBA from one to the other, and applies the
/// rules that sit between them: flatten transparency when the target cannot
/// carry it, and never overwrite anything the user did not ask to lose.
///
/// One failure does not stop a batch. Converting forty holiday photographs
/// should not be derailed by one truncated file, so every input is handled
/// independently and the failures are reported at the end.
/// </remarks>
public sealed class ConversionService
{
    private readonly FormatTable _table;
    private readonly CodecRegistry _registry;
    private readonly IFileSystem _fileSystem;
    private readonly OutputPathResolver _resolver;
    private readonly BackendSelector _selector;

    public ConversionService(FormatTable table, CodecRegistry registry, IFileSystem? fileSystem = null)
    {
        _table = table ?? throw new ArgumentNullException(nameof(table));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _fileSystem = fileSystem ?? PhysicalFileSystem.Instance;
        _resolver = new OutputPathResolver(_fileSystem);
        _selector = new BackendSelector(_table);
    }

    public ConversionSummary Convert(
        IReadOnlyList<string> inputPaths,
        TargetFormat target,
        ConversionOptions options,
        IProgress<ConversionResult>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(inputPaths);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(options);

        var results = new List<ConversionResult>(inputPaths.Count);

        foreach (var input in inputPaths)
        {
            var result = ConvertOne(input, target, options);
            results.Add(result);
            progress?.Report(result);
        }

        return new ConversionSummary(results);
    }

    public ConversionResult ConvertOne(string inputPath, TargetFormat target, ConversionOptions options)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            return ConvertCore(inputPath, target, options);
        }
        catch (Exception ex)
        {
            // Deliberately broad. Anything a decoder can throw for one bad file
            // must not take the rest of the batch down with it.
            return ConversionResult.Failed(inputPath, ex.Message);
        }
    }

    private ConversionResult ConvertCore(string inputPath, TargetFormat target, ConversionOptions options)
    {
        if (!_fileSystem.FileExists(inputPath))
        {
            return ConversionResult.Failed(inputPath, "The file does not exist.");
        }

        if (!_table.TryGetForPath(inputPath, out var source))
        {
            var extension = Path.GetExtension(inputPath);
            var described = string.IsNullOrEmpty(extension) ? "no extension" : extension;
            return ConversionResult.Skipped(inputPath, $"Recode does not handle {described} files.");
        }

        if (!source.CanRead)
        {
            return ConversionResult.Skipped(inputPath, $"{source.DisplayName} files cannot be used as a source.");
        }

        // Throws if the pair is impossible. Nothing has been touched on disk yet.
        _ = _selector.Select(source, target.Format);

        var decoder = _registry.GetDecoder(source);
        var encoder = _registry.GetEncoder(target.Format);

        var outputDirectory = options.OutputDirectory;
        if (!string.IsNullOrWhiteSpace(outputDirectory) && !_fileSystem.DirectoryExists(outputDirectory))
        {
            _fileSystem.CreateDirectory(outputDirectory);
        }

        var output = _resolver.Resolve(inputPath, target.Extension, outputDirectory, options.Force);

        if (output.SameAsInput && !options.Force)
        {
            // Should not be reachable: the resolver only returns the input path
            // when force was requested. Kept as a guard, because the one thing
            // this program must never do is destroy the original.
            return ConversionResult.Skipped(
                inputPath,
                "The output would replace the original. Use --force to recompress in place.");
        }

        var image = decoder.Decode(inputPath, source);

        // The source is fully in memory from here on, so writing over the input
        // is safe even when converting a file to its own format in place.
        if (!target.Format.SupportsAlpha)
        {
            AlphaFlattener.FlattenOntoWhite(image);
        }

        var quality = QualityRange.Resolve(options.Quality, target.Format);
        var encodeOptions = new EncodeOptions(quality);

        WriteAtomically(image, target, encoder, output, options.Force, encodeOptions);

        return ConversionResult.Converted(inputPath, output.Path);
    }

    /// <summary>
    /// Encodes to a temporary file beside the destination, then moves it into
    /// place.
    /// </summary>
    /// <remarks>
    /// Two reasons. A failure part way through encoding leaves the destination
    /// untouched rather than truncated, and converting a file to its own format
    /// in place cannot end with the original half overwritten by its own
    /// replacement.
    ///
    /// The temporary file goes in the destination directory so the move is a
    /// rename on the same volume rather than a copy.
    /// </remarks>
    private void WriteAtomically(
        RgbaImage image,
        TargetFormat target,
        IImageCodec encoder,
        OutputPath output,
        bool force,
        EncodeOptions encodeOptions)
    {
        var directory = Path.GetDirectoryName(output.Path);
        if (string.IsNullOrEmpty(directory))
        {
            directory = ".";
        }

        var temporary = Path.Combine(directory, $"recode-{Guid.NewGuid():N}.tmp");

        try
        {
            encoder.Encode(image, target.Format, temporary, encodeOptions);
            _fileSystem.MoveFile(temporary, output.Path, force);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            _fileSystem.DeleteFile(path);
        }
        catch (Exception)
        {
            // A leftover temporary file is untidy but not worth replacing the
            // real error with.
        }
    }
}
