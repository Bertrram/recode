using Recode.Core.Formats;

namespace Recode.Core.Registry;

/// <summary>
/// Builds the registry layout for the Explorer context menu from the format table.
/// </summary>
/// <remarks>
/// Nothing here is hard coded per format. Every readable extension gets a verb,
/// and every writable extension except the source's own format becomes an entry
/// in its submenu. Adding a format to formats.json therefore adds it to the
/// menu, and the install script does not have to change.
///
/// The cascade is built with SubCommands set to an empty string and a nested
/// shell key underneath, which is the way to get a submenu out of Explorer
/// without writing a COM handler. Submenu entries are ordered by key name, so
/// the names carry a numeric prefix.
///
/// This produces the classic context menu, which in Windows 11 lives under
/// "Show more options". That is a deliberate limit of this version.
/// </remarks>
public sealed class ContextMenuGenerator
{
    /// <summary>Explorer flag: draw a separator above this entry.</summary>
    private const int SeparatorBefore = 0x20;

    /// <summary>
    /// Hand every selected file to one invocation rather than starting a
    /// process per file. Without this, converting thirty photographs starts
    /// thirty copies of recode.exe at once.
    /// </summary>
    private const string MultiSelectPlayer = "Player";

    private const string MultiSelectSingle = "Single";

    private readonly FormatTable _table;
    private readonly ContextMenuOptions _options;

    public ContextMenuGenerator(FormatTable table, ContextMenuOptions options)
    {
        _table = table ?? throw new ArgumentNullException(nameof(table));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(options.ExecutablePath))
        {
            throw new ArgumentException("An executable path is required.", nameof(options));
        }
    }

    public ContextMenuPlan Generate()
    {
        var keys = new List<RegistryKeySpec>();
        var roots = new List<string>();

        foreach (var format in _table.Formats.Where(f => f.CanRead))
        {
            var submenu = _table.TargetsFor(format).ToList();

            foreach (var extension in format.Extensions)
            {
                var verbKey = VerbKeyFor(extension);
                roots.Add(verbKey);
                keys.Add(BuildVerbKey(verbKey));
                keys.AddRange(BuildSubmenu(verbKey, submenu));
            }
        }

        return new ContextMenuPlan(keys, roots);
    }

    private string VerbKeyFor(string extension) =>
        $@"{_options.ClassesRoot}\SystemFileAssociations\{extension}\shell\{_options.VerbKeyName}";

    private RegistryKeySpec BuildVerbKey(string verbKey)
    {
        return new RegistryKeySpec(verbKey, new[]
        {
            RegistryValueSpec.String("MUIVerb", _options.MenuLabel),
            RegistryValueSpec.String("Icon", _options.IconReference),

            // An empty SubCommands is what tells Explorer to read the nested
            // shell key below instead of looking for a command here.
            RegistryValueSpec.String("SubCommands", string.Empty),

            RegistryValueSpec.String("MultiSelectModel", MultiSelectPlayer)
        });
    }

    private IEnumerable<RegistryKeySpec> BuildSubmenu(string verbKey, IReadOnlyList<TargetFormat> targets)
    {
        var keys = new List<RegistryKeySpec>();
        var index = 1;

        foreach (var target in targets)
        {
            var itemKey = $@"{verbKey}\shell\{index:D2}-{target.Token}";

            keys.Add(new RegistryKeySpec(itemKey, new[]
            {
                RegistryValueSpec.String("MUIVerb", target.MenuLabel),
                RegistryValueSpec.String("Icon", _options.IconReference),
                RegistryValueSpec.String("MultiSelectModel", MultiSelectPlayer)
            }));

            keys.Add(new RegistryKeySpec($@"{itemKey}\command", new[]
            {
                RegistryValueSpec.Default(BuildConvertCommand(target))
            }));

            index++;
        }

        // The support entry sits at the bottom of the same submenu, behind a
        // separator, so Recode adds exactly one item to Explorer's menu rather
        // than two.
        var supportKey = $@"{verbKey}\shell\99-support";

        keys.Add(new RegistryKeySpec(supportKey, new[]
        {
            RegistryValueSpec.String("MUIVerb", _options.SupportLabel),
            RegistryValueSpec.String("Icon", _options.IconReference),
            RegistryValueSpec.Dword("CommandFlags", SeparatorBefore),
            RegistryValueSpec.String("MultiSelectModel", MultiSelectSingle)
        }));

        keys.Add(new RegistryKeySpec($@"{supportKey}\command", new[]
        {
            RegistryValueSpec.Default($"\"{_options.ExecutablePath}\" --about")
        }));

        return keys;
    }

    /// <summary>
    /// Builds the command line for one submenu entry.
    /// </summary>
    /// <remarks>
    /// The "--" separator matters. Explorer substitutes the selected files at
    /// %1, and with MultiSelectModel set to Player it appends the rest after
    /// it. Putting the separator first means every file arrives on the operand
    /// side of the command line, so a file named "--force.jpg" is treated as a
    /// file rather than as a flag.
    /// </remarks>
    private string BuildConvertCommand(TargetFormat target) =>
        $"\"{_options.ExecutablePath}\" --to {target.Token} -- \"%1\"";
}
