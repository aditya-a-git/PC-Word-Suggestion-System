using System.Diagnostics;
using PCWord.Core.Input;
using PCWord.Core.Suggestions;
using PCWord.Prediction.Diagnostics;
using PCWord.Prediction.Dictionary;
using PCWord.Prediction.Ranking;
using PCWord.Prediction.Inference;

namespace PCWord.Prediction.Context;

public sealed class CompletionUpdatedEventArgs(
    long contextVersion,
    string? fieldId,
    IReadOnlyList<string> candidates,
    CaretScreenBounds? caretBounds) : EventArgs
{
    public long ContextVersion { get; } = contextVersion;
    public string? FieldId { get; } = fieldId;
    public IReadOnlyList<string> Candidates { get; } = candidates;
    public CaretScreenBounds? CaretBounds { get; } = caretBounds;
}

/// <summary>
/// Connects versioned adapter snapshots to the existing local dictionary completion
/// engine. Prediction and delivery are synchronous; stale/equal versions are rejected
/// before they can replace the current result. It retains only current bounded state.
/// </summary>
public sealed class DictionaryCompletionCoordinator : IDisposable
{
    private const int MinimumPrefixLength = 2;
    private readonly IInputAdapter _adapter;
    private readonly CandidateRanker _ranker;
    private readonly PredictionContextManager _contextManager;
    private readonly CompletionMetrics _metrics;
    private readonly NGramModel? _nextWordModel;
    private bool _enabled = true;
    private bool _disposed;
    private IReadOnlyList<string> _currentCandidates = Array.Empty<string>();

    public DictionaryCompletionCoordinator(
        IInputAdapter adapter,
        CandidateRanker ranker,
        PredictionContextManager? contextManager = null,
        CompletionMetrics? metrics = null,
        NGramModel? nextWordModel = null)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(ranker);
        _adapter = adapter;
        _ranker = ranker;
        _contextManager = contextManager ?? new PredictionContextManager(
            InputContextSnapshot.MaximumTextBeforeCaret,
            InputContextSnapshot.MaximumTextAfterCaret);
        _metrics = metrics ?? new CompletionMetrics();
        _nextWordModel = nextWordModel;
        _adapter.ContextChanged += AdapterOnContextChanged;
        _adapter.SetEnabled(true);
    }

    public event EventHandler<CompletionUpdatedEventArgs>? SuggestionsChanged;

    public PredictionContext CurrentContext => _contextManager.Current;

    public bool IsEnabled => _enabled && !_disposed;

    public bool IsCurrent(long contextVersion, string? fieldId) =>
        !_disposed &&
        _contextManager.Current.Version == contextVersion &&
        StringComparer.Ordinal.Equals(_contextManager.Current.FieldId, fieldId);

    public void SetEnabled(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_enabled == enabled)
            return;

        _enabled = enabled;
        _currentCandidates = Array.Empty<string>();
        PredictionContext current = _contextManager.Current;
        _adapter.SetCandidateSet(current.Version, current.FieldId, _currentCandidates);
        if (!enabled)
            Deliver(current.Version, current.FieldId, _currentCandidates, null, null);
        _adapter.SetEnabled(enabled);
    }

    public void Dismiss()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        PredictionContext current = _contextManager.Current;
        _currentCandidates = Array.Empty<string>();
        Deliver(current.Version, current.FieldId, _currentCandidates, null, null);
    }

    public async ValueTask<TextEditResult> AcceptAsync(
        int candidateIndex,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        PredictionContext current = _contextManager.Current;
        if (!_enabled || current.FieldId is null ||
            candidateIndex < 0 || candidateIndex >= _currentCandidates.Count)
        {
            return new TextEditResult(TextEditStatus.Unsupported);
        }

        string candidate = _currentCandidates[candidateIndex];
        var request = new TextEditRequest(
            current.Version,
            current.FieldId,
            current.Prefix,
            candidate);

        TextEditResult result = await _adapter.ApplyEditAsync(request, cancellationToken);
        if (!result.WasApplied && IsCurrent(current.Version, current.FieldId))
            Dismiss();
        return result;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _adapter.ContextChanged -= AdapterOnContextChanged;
        _currentCandidates = Array.Empty<string>();
    }

    private void AdapterOnContextChanged(object? sender, InputContextChangedEventArgs args)
    {
        if (_disposed)
            return;

        InputContextSnapshot snapshot = args.Snapshot;
        long startedAt = snapshot.CaptureStartedTimestamp > 0
            ? snapshot.CaptureStartedTimestamp
            : Stopwatch.GetTimestamp();

        try
        {
            if (!_contextManager.TryUpdate(snapshot))
                return;
        }
        catch (ArgumentException)
        {
            _contextManager.Invalidate(snapshot.Version);
            _currentCandidates = Array.Empty<string>();
            Deliver(snapshot.Version, snapshot.FieldId, _currentCandidates, null, startedAt);
            return;
        }

        PredictionContext context = _contextManager.Current;
        if (!_enabled || (!context.CanPredictCompletion &&
            !(context.CanPredictNextWord && _nextWordModel is not null)) ||
            (context.CanPredictCompletion && context.Prefix.Length < MinimumPrefixLength))
        {
            _currentCandidates = Array.Empty<string>();
            Deliver(snapshot.Version, snapshot.FieldId, _currentCandidates, snapshot.CaretBounds, startedAt);
            return;
        }

        long lookupStartedAt = Stopwatch.GetTimestamp();
        IReadOnlyList<string> candidates;
        try
        {
            candidates = context.CanPredictNextWord
                ? _nextWordModel!.Predict(context.PrecedingText, SuggestionState.MaximumSuggestions)
                    .Select(word => FormatNextWordCasing(context.PrecedingText, word)).ToArray()
                : _ranker.GetSuggestions(context.Prefix, SuggestionState.MaximumSuggestions)
                    .Select(entry => SuggestionFormatter.MatchPrefixCasing(context.Prefix, entry.Word)).ToArray();
        }
        finally
        {
            _metrics.Record(context.CanPredictNextWord ? CompletionMetricStage.NGramLookup : CompletionMetricStage.DictionaryLookupAndRanking,
                Stopwatch.GetElapsedTime(lookupStartedAt));
        }

        _currentCandidates = candidates;
        Deliver(snapshot.Version, snapshot.FieldId, candidates, snapshot.CaretBounds, startedAt);
    }

    private static string FormatNextWordCasing(string precedingText, string word)
    {
        if (word.Length == 0) return word;
        int last = precedingText.Length - 1;
        while (last >= 0 && char.IsWhiteSpace(precedingText[last])) last--;
        bool sentenceStart = last < 0 || precedingText[last] is '.' or '!' or '?';
        return sentenceStart ? char.ToUpperInvariant(word[0]) + word[1..] : word;
    }

    private void Deliver(
        long version,
        string? fieldId,
        IReadOnlyList<string> candidates,
        CaretScreenBounds? caretBounds,
        long? endToEndStartedAt)
    {
        long deliveryStartedAt = Stopwatch.GetTimestamp();
        try
        {
            _adapter.SetCandidateSet(version, fieldId, candidates);
            SuggestionsChanged?.Invoke(this,
                new CompletionUpdatedEventArgs(version, fieldId, candidates, caretBounds));
        }
        finally
        {
            _metrics.Record(CompletionMetricStage.ResultDelivery,
                Stopwatch.GetElapsedTime(deliveryStartedAt));
            if (endToEndStartedAt is long startedAt)
                _metrics.Record(CompletionMetricStage.EndToEndPrediction,
                    Stopwatch.GetElapsedTime(startedAt));
        }
    }
}
