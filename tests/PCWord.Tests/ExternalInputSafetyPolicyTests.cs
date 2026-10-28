using PCWord.Core.Input;
using Xunit;

namespace PCWord.Tests;

public sealed class ExternalInputSafetyPolicyTests
{
    [Fact]
    public void DefaultsToPausedWithOnlyNotepadAllowlisted()
    {
        Assert.False(ExternalInputSafetyPolicy.DefaultEnabled);
        Assert.Equal(new[] { "notepad.exe" }, ExternalInputSafetyPolicy.DefaultAllowlist);
    }

    [Fact]
    public void ParsesHostNamesCaseInsensitivelyAndExactly()
    {
        Assert.True(ExternalInputSafetyPolicy.TryParseAllowlist(["notepad.exe"], out var allowlist));
        Assert.True(ExternalInputSafetyPolicy.IsHostAllowed(allowlist, "NOTEPAD.EXE"));
        Assert.False(ExternalInputSafetyPolicy.IsHostAllowed(allowlist, "notepad.exe.evil"));
        Assert.False(ExternalInputSafetyPolicy.IsHostAllowed(allowlist, "xnotepad.exe"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" notepad.exe")]
    [InlineData("notepad.exe ")]
    [InlineData("\u00A0notepad.exe")]
    [InlineData("C:\\Windows\\notepad.exe")]
    [InlineData("folder/notepad.exe")]
    [InlineData("*.exe")]
    [InlineData("notepad?.exe")]
    public void RejectsInvalidAllowlistEntries(string entry)
    {
        Assert.False(ExternalInputSafetyPolicy.TryParseAllowlist([entry], out _));
    }

    [Fact]
    public void MissingAllowlistIsInvalidButExplicitEmptyAllowlistIsValid()
    {
        Assert.False(ExternalInputSafetyPolicy.TryParseAllowlist(null, out _));
        Assert.True(ExternalInputSafetyPolicy.TryParseAllowlist([], out var allowlist));
        Assert.Empty(allowlist);
    }

    [Fact]
    public void HotKeyRegistrationFailureHasVisibleFallbackState()
    {
        Assert.Contains("Unavailable", PauseHotKeyStatus.Describe(PauseHotKeyRegistration.Unavailable));
        Assert.Contains("use the app toggle", PauseHotKeyStatus.Describe(PauseHotKeyRegistration.Unavailable));
        Assert.Contains("Registered", PauseHotKeyStatus.Describe(PauseHotKeyRegistration.Registered));
    }
}
