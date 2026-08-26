using Recode.Core.Abstractions;
using Recode.Core.Codecs;
using Recode.Core.Conversion;
using Recode.Core.Formats;

namespace Recode.Core.Diagnostics;

/// <summary>
/// Builds a <see cref="SupportReport"/> by asking each backend whether it works.
/// </summary>
/// <remarks>
/// Never throws. The support window exists to explain a broken installation, so
/// it has to be able to draw itself when the installation is broken.
/// </remarks>
public sealed class SupportProbe
{
    private readonly FormatTable _table;
    private readonly CodecRegistry _registry;

    public SupportProbe(FormatTable table, CodecRegistry registry)
    {
        _table = table ?? throw new ArgumentNullException(nameof(table));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public SupportReport Run()
    {
        var availability = new Dictionary<string, BackendAvailability>(StringComparer.OrdinalIgnoreCase);
        var backends = new List<BackendStatus>();

        foreach (var definition in _table.Backends)
        {
            var status = Probe(definition, availability);
            backends.Add(status);
        }

        var rows = new List<FormatSupportRow>();

        foreach (var format in _table.Formats)
        {
            var definition = _table.GetBackendFor(format);
            availability.TryGetValue(format.BackendId, out var backendAvailability);
            var available = backendAvailability?.Available ?? false;

            foreach (var extension in format.Extensions)
            {
                rows.Add(new FormatSupportRow
                {
                    DisplayName = format.MenuLabelFor(extension),
                    Extension = extension,
                    CanRead = format.CanRead,
                    CanWrite = format.CanWrite,
                    Backend = format.DescribeBackend(definition),
                    Available = available,
                    Problem = available ? null : DescribeProblem(backendAvailability)
                });
            }
        }

        return new SupportReport { Rows = rows, Backends = backends };
    }

    private BackendStatus Probe(BackendDefinition definition, Dictionary<string, BackendAvailability> availability)
    {
        BackendAvailability result;
        string? version = null;

        try
        {
            var codec = _registry.GetForBackend(definition.Id);
            result = codec.CheckAvailability();

            version = codec switch
            {
                HeifCodec heif => heif.TryGetVersion(),
                WebpCodec webp => webp.TryGetVersion(),
                _ => null
            };
        }
        catch (Exception ex)
        {
            result = BackendAvailability.Broken(ex.Message);
        }

        availability[definition.Id] = result;

        return new BackendStatus
        {
            Id = definition.Id,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            Bundled = definition.Bundled,
            Available = result.Available,
            Version = version,
            MissingLibrary = result.MissingLibrary,
            ExpectedLocation = result.ExpectedLocation,
            Problem = result.Available ? null : DescribeProblem(result)
        };
    }

    private static string DescribeProblem(BackendAvailability? availability)
    {
        if (availability is null)
        {
            return "The backend did not report a status.";
        }

        if (availability.MissingLibrary is not null && availability.ExpectedLocation is not null)
        {
            return $"{availability.MissingLibrary} is missing from {availability.ExpectedLocation}";
        }

        return availability.Detail ?? "Unavailable for an unknown reason.";
    }
}
