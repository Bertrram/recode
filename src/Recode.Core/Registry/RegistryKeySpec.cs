namespace Recode.Core.Registry;

public enum RegistryValueKind
{
    String,
    Dword
}

/// <summary>One registry value to write.</summary>
/// <param name="Name">Empty string means the key's default value.</param>
public sealed record RegistryValueSpec(string Name, RegistryValueKind Kind, string Value)
{
    public static RegistryValueSpec String(string name, string value) =>
        new(name, RegistryValueKind.String, value);

    public static RegistryValueSpec Default(string value) =>
        new(string.Empty, RegistryValueKind.String, value);

    public static RegistryValueSpec Dword(string name, int value) =>
        new(name, RegistryValueKind.Dword, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
}

/// <summary>One registry key and the values it carries.</summary>
public sealed record RegistryKeySpec(string Path, IReadOnlyList<RegistryValueSpec> Values);

/// <summary>
/// The full set of keys the context menu needs, plus the roots to delete when
/// uninstalling.
/// </summary>
/// <param name="Keys">Every key to create, in an order safe to apply top down.</param>
/// <param name="RootKeys">
/// The keys the installer owns outright. Removing these removes everything and
/// leaves nothing of Recode's behind.
/// </param>
public sealed record ContextMenuPlan(
    IReadOnlyList<RegistryKeySpec> Keys,
    IReadOnlyList<string> RootKeys);
