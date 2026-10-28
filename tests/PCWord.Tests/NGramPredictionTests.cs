using PCWord.Core.Input;
using PCWord.Prediction.Context;
using PCWord.Prediction.Dictionary;
using PCWord.Prediction.Inference;
using PCWord.Prediction.Ranking;
using Xunit;

namespace PCWord.Tests;

public sealed class NGramPredictionTests
{
    [Fact]
    public void UsesTrigramThenBigramAndUnigramBackoffWithDeterministicTies()
    {
        NGramModel model = NGramModel.Train(new[] { "red fox jumps", "red fox runs", "blue fox runs", "blue bird flies" });
        Assert.Equal(new[] { "runs", "jumps" }, model.Predict("red fox", 3).Take(2));
        Assert.Equal(new[] { "runs", "jumps" }, model.Predict("blue fox", 2));
        Assert.Equal(new[] { "blue", "bird", "flies" }, NGramModel.Train(new[] { "blue bird", "blue flies", "bird blue" }).Predict("unknown", 3, maximumOrder: 1));
    }

    [Fact]
    public void TokenizerNormalizesAndLineBoundariesResetCounts()
    {
        Assert.Equal(new[] { "café", "hello", "world" }, WordTokenizer.Tokenize("CAFÉ, Hello-world!"));
        NGramModel model = NGramModel.Train(new[] { "alpha beta", "gamma delta" });
        Assert.Equal("beta", model.Predict("alpha", 1)[0]);
        Assert.Equal("delta", model.Predict("gamma", 1)[0]);
        Assert.False(model.ContainsWord("missing"));
        NGramModel sentences = NGramModel.Train(new[] { "alpha beta. gamma delta" });
        Assert.Equal("beta", sentences.Predict("alpha", 1, maximumOrder: 2)[0]);
        Assert.Equal("delta", sentences.Predict("alpha beta. gamma", 1, maximumOrder: 3)[0]);
    }

    [Fact]
    public void EmptyModelHasNoSuggestionsAndBoundsVocabulary()
    {
        Assert.Empty(NGramModel.Train(Array.Empty<string>()).Predict("context"));
        NGramModel model = NGramModel.Train(new[] { "c b a" }, maximumVocabulary: 2);
        Assert.Equal(2, model.VocabularySize);
        Assert.Equal(new[] { "a", "b" }, model.PredictUnigram(3));
    }

    [Fact]
    public void SavesAndReloadsOnlyTheBoundedLocalModelAndSelectedConfiguration()
    {
        string path = Path.Combine(Path.GetTempPath(), $"pcword-{Guid.NewGuid():N}.json");
        try
        {
            NGramModel model = NGramModel.Train(new[] { "alpha beta", "alpha gamma" });
            model.ConfigurePrediction(.6, 2);
            model.Save(path);
            NGramModel loaded = NGramModel.Load(path);
            Assert.Equal(.6, loaded.RecommendedBackoff);
            Assert.Equal(2, loaded.RecommendedOrder);
            Assert.Equal(model.Predict("alpha", 3), loaded.Predict("alpha", 3));
            File.WriteAllText(path, "{}");
            Assert.Throws<InvalidDataException>(() => NGramModel.Load(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task ControlledCoordinatorOffersNextWordOnlyAtEligibleBoundaryAndKeepsFreshProvenance()
    {
        var adapter = new FakeAdapter();
        var model = NGramModel.Train(new[] { "hello world", "hello there", "hello world" });
        using var coordinator = new DictionaryCompletionCoordinator(adapter,
            new CandidateRanker(new PrefixIndex(Array.Empty<DictionaryEntry>())), nextWordModel: model);
        adapter.Publish(Snapshot(1, "hello "));
        Assert.Equal(new[] { "world", "there" }, adapter.Candidates.Take(2));
        adapter.Publish(Snapshot(2, "hello wor"));
        Assert.Empty(adapter.Candidates); // no partial-prefix dictionary fixture
        adapter.Publish(Snapshot(3, "hello ", sensitivity: InputFieldSensitivity.Sensitive));
        Assert.Empty(adapter.Candidates);
        adapter.Publish(Snapshot(4, "hello "));
        Assert.NotEmpty(adapter.Candidates);
        Assert.True((await coordinator.AcceptAsync(0)).WasApplied);
        Assert.Equal(string.Empty, adapter.LastRequest!.ExpectedText);
        Assert.Equal(4, adapter.LastRequest.ContextVersion);
    }

    [Theory]
    [InlineData(false, true, InputFieldSensitivity.KnownSafe, 0, false)]
    [InlineData(true, false, InputFieldSensitivity.KnownSafe, 0, false)]
    [InlineData(true, true, InputFieldSensitivity.Unknown, 0, false)]
    [InlineData(true, true, InputFieldSensitivity.Sensitive, 0, false)]
    [InlineData(true, true, InputFieldSensitivity.KnownSafe, 1, false)]
    [InlineData(true, true, InputFieldSensitivity.KnownSafe, 0, true)]
    public void NextWordPredictionFailsClosedForUnavailableOrIneligibleContext(
        bool available, bool focus, InputFieldSensitivity sensitivity, int selection, bool composing)
    {
        var adapter = new FakeAdapter();
        var model = NGramModel.Train(new[] { "hello world", "hello there" });
        using var coordinator = new DictionaryCompletionCoordinator(adapter,
            new CandidateRanker(new PrefixIndex(Array.Empty<DictionaryEntry>())), nextWordModel: model);
        adapter.Publish(Snapshot(1, "hello ", available: available, focus: focus,
            sensitivity: sensitivity, selection: selection, composing: composing));
        Assert.Empty(adapter.Candidates);
    }

    [Fact]
    public async Task PauseRequiresFreshBoundaryAndOldCandidateCannotBeAccepted()
    {
        var adapter = new FakeAdapter();
        var model = NGramModel.Train(new[] { "hello world", "hello there" });
        using var coordinator = new DictionaryCompletionCoordinator(adapter,
            new CandidateRanker(new PrefixIndex(Array.Empty<DictionaryEntry>())), nextWordModel: model);
        adapter.Publish(Snapshot(1, "hello "));
        var obsolete = new TextEditRequest(1, "field", string.Empty, "world");
        coordinator.SetEnabled(false);
        coordinator.SetEnabled(true);
        Assert.Empty(adapter.Candidates);
        Assert.Equal(TextEditStatus.Unsupported, (await coordinator.AcceptAsync(0)).Status);
        adapter.Publish(Snapshot(2, "hello "));
        Assert.NotEmpty(adapter.Candidates);
        adapter.Publish(Snapshot(3, "hello ", fieldId: "changed"));
        Assert.NotEmpty(adapter.Candidates);
        Assert.True(coordinator.IsCurrent(3, "changed"));
        Assert.Equal(TextEditStatus.StaleContext, (await adapter.ApplyEditAsync(obsolete)).Status);
        Assert.Null(adapter.LastAppliedRequest);
    }

    [Theory]
    [InlineData("hello ", ",")]
    [InlineData("hello ", "   ")]
    [InlineData("", "")]
    public void ValidatorAllowsSafeZeroPrefixInsertionAndPreservesSuffix(string before, string after)
    {
        var snapshot = Snapshot(1, before, after);
        var request = new TextEditRequest(1, "field", "", "world");
        Assert.True(TextEditValidator.Validate(snapshot, request, 1, new[] { "world" }).IsValid);
    }

    [Fact]
    public void ZeroPrefixInsertionRejectsInsideWordStaleOrSensitiveContext()
    {
        Assert.False(TextEditValidator.Validate(Snapshot(1, "hello", "world"), new(1, "field", "", "world"), 1, new[] { "world" }).IsValid);
        Assert.False(TextEditValidator.Validate(Snapshot(2, "hello "), new(1, "field", "", "world"), 2, new[] { "world" }).IsValid);
        Assert.Equal(TextEditStatus.RefusedSensitiveField, TextEditValidator.Validate(
            Snapshot(1, "hello ", sensitivity: InputFieldSensitivity.Sensitive), new(1, "field", "", "world"), 1, new[] { "world" }).RefusalStatus);
    }

    private static InputContextSnapshot Snapshot(long version, string before, string after = "",
        InputFieldSensitivity sensitivity = InputFieldSensitivity.KnownSafe, bool available = true,
        bool focus = true, int selection = 0, bool composing = false, string fieldId = "field") =>
        new(version, fieldId, before, after, selection, focus, sensitivity, composing, available);

    private sealed class FakeAdapter : IInputAdapter
    {
        private InputContextSnapshot? _snapshot;
        private IReadOnlyList<string> _candidates = Array.Empty<string>();
        public event EventHandler<InputContextChangedEventArgs>? ContextChanged;
        public bool Enabled { get; private set; } = true;
        public IReadOnlyList<string> Candidates => _candidates;
        public TextEditRequest? LastRequest { get; private set; }
        public TextEditRequest? LastAppliedRequest { get; private set; }
        public void Publish(InputContextSnapshot value) { _snapshot = value; if (Enabled) ContextChanged?.Invoke(this, new(value)); }
        public void SetEnabled(bool enabled) => Enabled = enabled;
        public void SetCandidateSet(long version, string? field, IReadOnlyList<string> candidates) =>
            _candidates = _snapshot?.Version == version && _snapshot.FieldId == field ? candidates.ToArray() : Array.Empty<string>();
        public ValueTask<TextEditResult> ApplyEditAsync(TextEditRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (_snapshot is null) return ValueTask.FromResult(new TextEditResult(TextEditStatus.StaleContext));
            var validation = TextEditValidator.Validate(_snapshot, request, _snapshot.Version, _candidates);
            if (validation.IsValid) LastAppliedRequest = request;
            return ValueTask.FromResult(new TextEditResult(validation.IsValid ? TextEditStatus.Applied : validation.RefusalStatus!.Value));
        }
    }
}
