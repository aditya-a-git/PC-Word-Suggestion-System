using System.Text.Json;
using System.Text.RegularExpressions;

namespace PCWord.Core;

public sealed record ShortcutSpec(string Key, bool Control = false, bool Alt = false, bool Shift = false)
{
    public static ShortcutSpec DefaultPause { get; } = new("P", true, true, true);
    public static ShortcutSpec DefaultAcceptance { get; } = new("Enter", true, true, false);

    public bool IsValid(out string error)
    {
        error = "";
        if (!Control && !Alt && !Shift) { error = "A shortcut needs Ctrl, Alt, or Shift."; return false; }
        if (!Regex.IsMatch(Key, "^([A-Z]|F([1-9]|1[0-2])|Enter|Space|Tab|Escape|Backspace|Delete|Insert|Home|End|PageUp|PageDown|Up|Down|Left|Right|[0-9])$", RegexOptions.IgnoreCase)) { error = "Choose a supported non-printable or single alphanumeric key."; return false; }
        string k = Key.ToUpperInvariant();
        if (k == "F4" && Alt || k == "DELETE" && Control && Alt) { error = "This is a reserved system shortcut."; return false; }
        return true;
    }
    public override string ToString() => $"{(Control ? "Ctrl+" : "")}{(Alt ? "Alt+" : "")}{(Shift ? "Shift+" : "")}{Key}";
}

public sealed record UserSettings(int SchemaVersion = 1, bool Enabled = false,
    ShortcutSpec? PauseShortcut = null, ShortcutSpec? AcceptanceShortcut = null,
    string[]? HostAllowlist = null, bool PersonalizationEnabled = false)
{
    public static UserSettings Defaults { get; } = new(1, false, ShortcutSpec.DefaultPause, ShortcutSpec.DefaultAcceptance, ["notepad.exe"], false);
    public ShortcutSpec Pause => PauseShortcut ?? ShortcutSpec.DefaultPause;
    public ShortcutSpec Acceptance => AcceptanceShortcut ?? ShortcutSpec.DefaultAcceptance;
    public string[] Hosts => HostAllowlist ?? ["notepad.exe"];
    public bool IsValid(out string error)
    {
        error = "";
        if (SchemaVersion != 1) { error = "Unknown settings version."; return false; }
        if (!Pause.IsValid(out error) || !Acceptance.IsValid(out error)) return false;
        if (Pause == Acceptance) { error = "Pause and acceptance shortcuts must differ."; return false; }
        if (Hosts.Length is < 1 or > 64 || Hosts.Any(h => h.Length > 255 || !Regex.IsMatch(h, "^[A-Za-z0-9][A-Za-z0-9._-]*\\.exe$", RegexOptions.IgnoreCase))) { error = "The host allowlist contains an invalid executable name."; return false; }
        if (Hosts.Distinct(StringComparer.OrdinalIgnoreCase).Count() != Hosts.Length) { error = "The host allowlist contains duplicates."; return false; }
        return true;
    }
}

public interface IUserSettingsStore
{
    (UserSettings Settings, string? Status) Load();
    void Save(UserSettings settings);
}

public sealed class JsonUserSettingsStore(string path) : IUserSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip };
    public (UserSettings Settings, string? Status) Load()
    {
        try
        {
            if (!File.Exists(path)) return (UserSettings.Defaults, null);
            var settings = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(path), JsonOptions);
            if (settings is null) return (UserSettings.Defaults, "Settings were invalid; safe defaults loaded.");
            if (!settings.IsValid(out var error)) return (UserSettings.Defaults, $"Settings were invalid; safe defaults loaded. {error}");
            return (settings, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { return (UserSettings.Defaults, "Settings could not be read; safe defaults loaded."); }
    }
    public void Save(UserSettings settings)
    {
        if (!settings.IsValid(out var error)) throw new ArgumentException(error, nameof(settings));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) JsonSerializer.Serialize(stream, settings, JsonOptions);
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

