using PCWord.Core.Input;
using PCWord.Prediction.Context;
using PCWord.Prediction.Dictionary;
using PCWord.Prediction.Ranking;
using Xunit;

namespace PCWord.Tests;

public sealed class DictionaryCompletionCoordinatorTests
{
    [Fact]
    public void ConvertsSafeSnapshotsIntoBoundedDictionaryCandidates()
    {
        var adapter = new FakeInputAdapter();
        using var coordinator = new DictionaryCompletionCoordinator(adapter, CreateRanker());
        CompletionUpdatedEventArgs? update = null;
        coordinator.SuggestionsChanged += (_, args) => update = args;

        adapter.Publish(Snapshot(1, "typed pro"));

        Assert.NotNull(update);
        Assert.Equal(1, update.ContextVersion);
        Assert.Equal("field-a", update.FieldId);
        Assert.Equal(new[] { "program", "progress", "project" }, update.Candidates);
        Assert.Equal(update.Candidates, adapter.Candidates);
    }

    [Theory]
    [InlineData(false, InputFieldSensitivity.KnownSafe, 0, false, true, "typed pro", "")]
    [InlineData(true, InputFieldSensitivity.Unknown, 0, false, true, "typed pro", "")]
    [InlineData(true, InputFieldSensitivity.Sensitive, 0, false, true, "typed pro", "")]
    [InlineData(true, InputFieldSensitivity.KnownSafe, 1, false, true, "typed pro", "")]
    [InlineData(true, InputFieldSensitivity.KnownSafe, 0, true, true, "typed pro", "")]
    [InlineData(true, InputFieldSensitivity.KnownSafe, 0, false, false, null, null)]
    [InlineData(true, InputFieldSensitivity.KnownSafe, 0, false, true, "typed pro", "ject")]
    public void IneligibleSnapshotsClearCandidates(
        bool focus,
        InputFieldSensitivity sensitivity,
        int selection,
        bool composing,
        bool available,
        string? before,
        string? after)
    {
        var adapter = new FakeInputAdapter();
        using var coordinator = new DictionaryCompletionCoordinator(adapter, CreateRanker());
        int visibleCandidateCount = -1;
        coordinator.SuggestionsChanged += (_, args) => visibleCandidateCount = args.Candidates.Count;

        adapter.Publish(Snapshot(1, before, after, focus: focus,
            sensitivity: sensitivity, selectionLength: selection,
            composing: composing, available: available));

        Assert.Equal(0, visibleCandidateCount);
        Assert.Empty(adapter.Candidates);
    }

    [Fact]
    public void IgnoresStaleSnapshotAndDoesNotRollCandidateVersionBack()
    {
        var adapter = new FakeInputAdapter();
        using var coordinator = new DictionaryCompletionCoordinator(adapter, CreateRanker());
        adapter.Publish(Snapshot(5, "typed pro"));
        adapter.Publish(Snapshot(4, "typed pro"));

        Assert.Equal(5, coordinator.CurrentContext.Version);
        Assert.Equal(5, adapter.CandidateVersion);
        Assert.Equal(new[] { "program", "progress", "project" }, adapter.Candidates);
    }

    [Fact]
    public async Task AcceptUsesCurrentFieldVersionPrefixAndCandidateProvenance()
    {
        var adapter = new FakeInputAdapter();
        using var coordinator = new DictionaryCompletionCoordinator(adapter, CreateRanker());
        adapter.Publish(Snapshot(2, "typed pro"));

        TextEditResult result = await coordinator.AcceptAsync(1);

        Assert.True(result.WasApplied);
        Assert.Equal(new TextEditRequest(2, "field-a", "pro", "progress"), adapter.LastRequest);
    }

    [Fact]
    public async Task RejectsCandidateOutsideTheCurrentAdapterCandidateSet()
    {
        var adapter = new FakeInputAdapter();
        using var coordinator = new DictionaryCompletionCoordinator(adapter, CreateRanker());
        adapter.Publish(Snapshot(2, "typed pro"));
        var invalid = new TextEditRequest(2, "field-a", "pro", "prose");

        TextEditResult result = await adapter.ApplyEditAsync(invalid);

        Assert.Equal(TextEditStatus.Failed, result.Status);
    }

    [Fact]
    public void DismissClearsCurrentCandidatesUntilAChangedContextArrives()
    {
        var adapter = new FakeInputAdapter();
        using var coordinator = new DictionaryCompletionCoordinator(adapter, CreateRanker());
        adapter.Publish(Snapshot(1, "typed pro"));

        coordinator.Dismiss();

        Assert.Empty(adapter.Candidates);
        Assert.Empty(adapter.LastPublishedCandidates);

        adapter.Publish(Snapshot(2, "typed prog"));

        Assert.NotEmpty(adapter.Candidates);
    }

    [Fact]
    public void GlobalDisableStopsCollectionAndClearsCandidates()
    {
        var adapter = new FakeInputAdapter();
        using var coordinator = new DictionaryCompletionCoordinator(adapter, CreateRanker());
        adapter.Publish(Snapshot(1, "typed pro"));

        coordinator.SetEnabled(false);
        adapter.Publish(Snapshot(2, "typed prog"));

        Assert.False(adapter.Enabled);
        Assert.Equal(1, coordinator.CurrentContext.Version);
        Assert.Empty(adapter.Candidates);
        Assert.Empty(adapter.LastPublishedCandidates);
    }


    [Fact]
    public void RapidUpdatesDeliverOnlyResultsBoundToTheLatestVersionAndField()
    {
        var adapter = new FakeInputAdapter();
        using var coordinator = new DictionaryCompletionCoordinator(adapter, CreateRanker());
        var delivered = new List<CompletionUpdatedEventArgs>();
        coordinator.SuggestionsChanged += (_, args) =>
        {
            Assert.True(coordinator.IsCurrent(args.ContextVersion, args.FieldId));
            delivered.Add(args);
        };

        adapter.Publish(Snapshot(1, "typed pro", fieldId: "field-a"));
        adapter.Publish(Snapshot(2, "typed pre", fieldId: "field-b"));
        adapter.Publish(Snapshot(3, "typed xyz", fieldId: "field-b"));

        Assert.Equal(new long[] { 1, 2, 3 }, delivered.Select(item => item.ContextVersion));
        Assert.Equal("field-b", delivered[^1].FieldId);
        Assert.Empty(delivered[^1].Candidates);
        Assert.Empty(adapter.Candidates);
        Assert.False(coordinator.IsCurrent(2, "field-a"));
        Assert.True(coordinator.IsCurrent(3, "field-b"));
    }

    [Fact]
    public async Task FieldOrFocusChangeInvalidatesOldCandidateAndCannotAcceptIt()
    {
        var adapter = new FakeInputAdapter();
        using var coordinator = new DictionaryCompletionCoordinator(adapter, CreateRanker());
        adapter.Publish(Snapshot(1, "typed pro", fieldId: "field-a"));
        TextEditRequest oldRequest = new(1, "field-a", "pro", "program");

        adapter.Publish(Snapshot(2, "typed pro", focus: false, fieldId: "field-b"));

        Assert.Empty(adapter.Candidates);
        Assert.Equal(TextEditStatus.Unsupported, (await coordinator.AcceptAsync(0)).Status);
        Assert.Equal(TextEditStatus.StaleContext, (await adapter.ApplyEditAsync(oldRequest)).Status);
        Assert.Null(adapter.LastAppliedRequest);
    }

    [Fact]
    public async Task DisableAndReenableDoesNotReviveCandidatesUntilFreshSnapshot()
    {
        var adapter = new FakeInputAdapter();
        using var coordinator = new DictionaryCompletionCoordinator(adapter, CreateRanker());
        adapter.Publish(Snapshot(1, "typed pro"));

        coordinator.SetEnabled(false);
        Assert.False(coordinator.IsEnabled);
        Assert.Empty(adapter.Candidates);
        adapter.Publish(Snapshot(2, "typed prog"));
        coordinator.SetEnabled(true);

        Assert.True(coordinator.IsEnabled);
        Assert.Empty(adapter.Candidates);
        Assert.Equal(TextEditStatus.Unsupported, (await coordinator.AcceptAsync(0)).Status);

        adapter.Publish(Snapshot(3, "typed prog"));
        Assert.NotEmpty(adapter.Candidates);
        Assert.True(coordinator.IsCurrent(3, "field-a"));
    }

    [Fact]
    public async Task EmptyResultClearsVisibleCandidatesAndRejectsFormerCandidate()
    {
        var adapter = new FakeInputAdapter();
        using var coordinator = new DictionaryCompletionCoordinator(adapter, CreateRanker());
        CompletionUpdatedEventArgs? latest = null;
        coordinator.SuggestionsChanged += (_, args) => latest = args;
        adapter.Publish(Snapshot(1, "typed pro"));
        TextEditRequest former = new(1, "field-a", "pro", "program");

        adapter.Publish(Snapshot(2, "typed xyz"));

        Assert.NotNull(latest);
        Assert.Equal(2, latest.ContextVersion);
        Assert.Empty(latest.Candidates);
        Assert.Empty(adapter.Candidates);
        Assert.Equal(TextEditStatus.StaleContext, (await adapter.ApplyEditAsync(former)).Status);
    }

    private static CandidateRanker CreateRanker() => new(new PrefixIndex(new[]
    {
        new DictionaryEntry("program", 40),
        new DictionaryEntry("progress", 30),
        new DictionaryEntry("project", 20),
        new DictionaryEntry("propose", 10),
        new DictionaryEntry("prelude", 5)
    }));

    private static InputContextSnapshot Snapshot(
        long version,
        string? before,
        string? after = "",
        bool focus = true,
        InputFieldSensitivity sensitivity = InputFieldSensitivity.KnownSafe,
        int selectionLength = 0,
        bool composing = false,
        bool available = true,
        string fieldId = "field-a") =>
        new(version, fieldId, before, after, selectionLength, focus, sensitivity,
            composing, available);

    private sealed class FakeInputAdapter : IInputAdapter
    {
        private InputContextSnapshot? _snapshot;

        public event EventHandler<InputContextChangedEventArgs>? ContextChanged;
        public bool Enabled { get; private set; } = true;
        public long CandidateVersion { get; private set; } = -1;
        public string? CandidateFieldId { get; private set; }
        public IReadOnlyList<string> Candidates { get; private set; } = Array.Empty<string>();
        public IReadOnlyList<string> LastPublishedCandidates { get; private set; } = Array.Empty<string>();
        public TextEditRequest? LastRequest { get; private set; }
        public TextEditRequest? LastAppliedRequest { get; private set; }

        public void Publish(InputContextSnapshot snapshot)
        {
            if (!Enabled)
                return;
            _snapshot = snapshot;
            ContextChanged?.Invoke(this, new InputContextChangedEventArgs(snapshot));
        }

        public void SetEnabled(bool enabled) => Enabled = enabled;

        public void SetCandidateSet(long contextVersion, string? fieldId, IReadOnlyList<string> candidates)
        {
            if (_snapshot is null || _snapshot.Version != contextVersion ||
                !StringComparer.Ordinal.Equals(_snapshot.FieldId, fieldId))
            {
                Candidates = Array.Empty<string>();
                CandidateVersion = -1;
                CandidateFieldId = null;
                LastPublishedCandidates = Candidates;
                return;
            }

            CandidateVersion = contextVersion;
            CandidateFieldId = fieldId;
            Candidates = candidates.ToArray();
            LastPublishedCandidates = Candidates;
        }

        public ValueTask<TextEditResult> ApplyEditAsync(
            TextEditRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (cancellationToken.IsCancellationRequested)
                return ValueTask.FromResult(new TextEditResult(TextEditStatus.Cancelled));
            if (_snapshot is null || request.ContextVersion != CandidateVersion ||
                !StringComparer.Ordinal.Equals(request.FieldId, CandidateFieldId))
                return ValueTask.FromResult(new TextEditResult(TextEditStatus.StaleContext));

            TextEditValidationResult validation = TextEditValidator.Validate(
                _snapshot, request, _snapshot.Version, Candidates);
            if (validation.IsValid)
                LastAppliedRequest = request;
            return ValueTask.FromResult(validation.IsValid
                ? new TextEditResult(TextEditStatus.Applied)
                : new TextEditResult(validation.RefusalStatus ?? TextEditStatus.Failed));
        }
    }
}
