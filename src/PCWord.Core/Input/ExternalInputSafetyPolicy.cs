namespace PCWord.Core.Input;

/// <summary>Managed settings parsing shared with the Windows integration UI.</summary>
public static class ExternalInputSafetyPolicy
{
    public static IReadOnlyList<string> DefaultAllowlist { get; } = Array.AsReadOnly(["notepad.exe"]);

    public static bool DefaultEnabled => false;

    public static bool TryParseAllowlist(IEnumerable<string>? entries, out IReadOnlySet<string> allowlist)
    {
        var parsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (entries is null)
        {
            allowlist = parsed;
            return false;
        }

        foreach (string? entry in entries)
        {
            if (!IsValidExecutableName(entry))
            {
                allowlist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                return false;
            }
            parsed.Add(entry!);
        }

        allowlist = parsed;
        return true;
    }

    public static bool IsValidExecutableName(string? name)
    {
        if (string.IsNullOrEmpty(name) || !StringComparer.Ordinal.Equals(name, name.Trim()))
            return false;

        foreach (char character in name)
        {
            if (char.IsWhiteSpace(character) || character is '\\' or '/' or '*' or '?')
                return false;
        }
        return true;
    }

    public static bool IsHostAllowed(IReadOnlySet<string> allowlist, string? executableName) =>
        allowlist is not null && IsValidExecutableName(executableName) && allowlist.Contains(executableName!);
}

public enum PauseHotKeyRegistration
{
    Unavailable,
    Registered
}

public static class PauseHotKeyStatus
{
    public static string Describe(PauseHotKeyRegistration registration) => registration switch
    {
        PauseHotKeyRegistration.Registered => "Registered: Ctrl+Alt+Shift+P",
        _ => "Unavailable: Ctrl+Alt+Shift+P could not be registered; use the app toggle."
    };
}
