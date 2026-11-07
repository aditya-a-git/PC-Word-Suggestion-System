using PCWord.Core;
using Xunit;

namespace PCWord.Tests;

public sealed class UserSettingsServiceTests
{
    [Fact]
    public void UpdatePersistsBeforePublishingAndReturnsDefensiveSnapshots()
    {
        var store = new MemorySettingsStore();
        var service = new UserSettingsService(store);
        UserSettingsChangedEventArgs? change = null;
        service.Changed += (_, args) =>
        {
            Assert.True(store.SaveCount > 0);
            change = args;
        };

        var candidate = UserSettings.Defaults with { Enabled = true, HostAllowlist = ["notepad.exe", "wordpad.exe"] };
        Assert.True(service.TryUpdate(candidate, out var error), error);
        Assert.True(service.Current.Enabled);
        Assert.True(change?.Current.Enabled);
        Assert.False(change?.Previous.Enabled);
        Assert.Equal(["notepad.exe", "wordpad.exe"], service.Current.Hosts);

        var exposed = service.Current.HostAllowlist!;
        exposed[0] = "changed.exe";
        Assert.Equal("notepad.exe", service.Current.Hosts[0]);
    }

    [Fact]
    public void InvalidSettingsAreNeitherPersistedNorPublished()
    {
        var store = new MemorySettingsStore();
        var service = new UserSettingsService(store);
        int notifications = 0;
        service.Changed += (_, _) => notifications++;

        Assert.False(service.TryUpdate(UserSettings.Defaults with { HostAllowlist = ["not a path.exe"] }, out var error));
        Assert.NotNull(error);
        Assert.Equal(0, store.SaveCount);
        Assert.Equal(0, notifications);
        Assert.False(service.Current.Enabled);
    }

    [Fact]
    public void EquivalentSettingsDoNotRewriteOrNotify()
    {
        var store = new MemorySettingsStore();
        var service = new UserSettingsService(store);
        int notifications = 0;
        service.Changed += (_, _) => notifications++;

        Assert.True(service.TryUpdate(UserSettings.Defaults with { HostAllowlist = ["NOTEPAD.EXE"] }, out _));
        Assert.Equal(0, store.SaveCount);
        Assert.Equal(0, notifications);
    }

    private sealed class MemorySettingsStore : IUserSettingsStore
    {
        public UserSettings Value { get; private set; } = UserSettings.Defaults;
        public int SaveCount { get; private set; }
        public (UserSettings Settings, string? Status) Load() => (Value, null);
        public void Save(UserSettings settings) { Value = settings; SaveCount++; }
    }
}