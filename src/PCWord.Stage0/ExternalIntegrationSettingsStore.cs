using Microsoft.Win32;
using PCWord.Core.Input;
using System.Security;

namespace PCWord.Stage0;

internal sealed record ExternalIntegrationSettings(bool IsValid, bool Enabled,
    IReadOnlySet<string> Allowlist, string? Error)
{
    public static ExternalIntegrationSettings Invalid(string error) => new(false, false,
        new HashSet<string>(StringComparer.OrdinalIgnoreCase), error);
}

internal static class ExternalIntegrationSettingsStore
{
    private const string RegistryPath = @"Software\PCWord";

    public static ExternalIntegrationSettings LoadOrCreateDefaults()
    {
        try
        {
            using RegistryKey? existing = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: false);
            if (existing is null)
            {
                using RegistryKey created = Registry.CurrentUser.CreateSubKey(RegistryPath, writable: true);
                created.SetValue("Enabled", 0, RegistryValueKind.DWord);
                created.SetValue("Allowlist", ExternalInputSafetyPolicy.DefaultAllowlist.ToArray(), RegistryValueKind.MultiString);
            }

            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: false);
            if (key is null)
                return ExternalIntegrationSettings.Invalid("Settings could not be opened; external context is denied.");
            if (key.GetValueKind("Enabled") != RegistryValueKind.DWord ||
                key.GetValue("Enabled", null, RegistryValueOptions.DoNotExpandEnvironmentNames) is not int enabled ||
                enabled is not (0 or 1))
                return ExternalIntegrationSettings.Invalid("Enabled setting is missing or malformed; external context is denied.");
            if (key.GetValueKind("Allowlist") != RegistryValueKind.MultiString ||
                key.GetValue("Allowlist", null, RegistryValueOptions.DoNotExpandEnvironmentNames) is not string[] entries ||
                !ExternalInputSafetyPolicy.TryParseAllowlist(entries, out IReadOnlySet<string> allowlist))
                return ExternalIntegrationSettings.Invalid("Allowlist setting is missing or malformed; external context is denied.");

            return new ExternalIntegrationSettings(true, enabled == 1, allowlist, null);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or ArgumentException or SecurityException)
        {
            return ExternalIntegrationSettings.Invalid("Settings are unreadable; external context is denied.");
        }
    }

    public static bool TrySetEnabled(bool enabled)
    {
        try
        {
            ExternalIntegrationSettings current = LoadOrCreateDefaults();
            if (!current.IsValid) return false;
            using RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: true)
                ?? throw new IOException("Settings key unavailable.");
            key.SetValue("Enabled", enabled ? 1 : 0, RegistryValueKind.DWord);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or ArgumentException or SecurityException)
        {
            return false;
        }
    }
}
