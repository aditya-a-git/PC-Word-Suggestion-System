using PCWord.Core;
using Xunit;

namespace PCWord.Tests;

public sealed class UserSettingsTests
{
    [Fact] public void DefaultsArePausedAndPersonalizationOff() { Assert.False(UserSettings.Defaults.Enabled); Assert.False(UserSettings.Defaults.PersonalizationEnabled); Assert.Equal(["notepad.exe"], UserSettings.Defaults.Hosts); }

    [Fact] public void SettingsRoundTripAndUnknownFieldsAreIgnored()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); string file = Path.Combine(dir, "settings.json");
        try
        {
            var store = new JsonUserSettingsStore(file); var expected = UserSettings.Defaults with { Enabled = true };
            store.Save(expected); Assert.Equal(expected, store.Load().Settings);
            File.WriteAllText(file, File.ReadAllText(file).Replace("{", "{\"FutureFlag\":true,", StringComparison.Ordinal));
            Assert.True(store.Load().Settings.Enabled);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"SchemaVersion\":99,\"Enabled\":true}")]
    [InlineData("{\"SchemaVersion\":1,\"Enabled\":true,\"HostAllowlist\":[\"bad name.exe\"]}")]
    public void InvalidFilesReturnSafeDefaults(string json)
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); string file = Path.Combine(dir, "settings.json");
        try { File.WriteAllText(file, json); var result = new JsonUserSettingsStore(file).Load(); Assert.False(result.Settings.Enabled); Assert.False(result.Settings.PersonalizationEnabled); Assert.NotNull(result.Status); }
        finally { Directory.Delete(dir, true); }
    }

    [Fact] public void MissingFileReturnsSafeDefaults() { var result = new JsonUserSettingsStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json")).Load(); Assert.False(result.Settings.Enabled); Assert.Null(result.Status); }
    [Theory] [InlineData("F4", false, true, false)] [InlineData("Delete", true, true, false)] [InlineData("A", false, false, false)]
    public void ShortcutValidationRejectsReservedAndBareKeys(string key, bool control, bool alt, bool shift) => Assert.False(new ShortcutSpec(key, control, alt, shift).IsValid(out _));
    [Fact] public void DuplicateShortcutsAreRejected() => Assert.False((UserSettings.Defaults with { AcceptanceShortcut = UserSettings.Defaults.PauseShortcut }).IsValid(out _));
    [Fact] public void SaveRejectsInvalidSettings() => Assert.Throws<ArgumentException>(() => new JsonUserSettingsStore(Path.Combine(Path.GetTempPath(), "never", "settings.json")).Save(UserSettings.Defaults with { Enabled = true, HostAllowlist = ["bad"] }));
}

