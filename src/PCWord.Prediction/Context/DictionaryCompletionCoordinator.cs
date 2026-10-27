using PCWord.Core.Input;
using PCWord.Core.Suggestions;
using PCWord.Prediction.Dictionary;
using PCWord.Prediction.Ranking;

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
/// engine. It retains only the current bounded prediction context and candidates.
/// </summary>
public sealed class DictionaryCompletionCoordinator : IDisposable
{
    private const int MinimumPrefixLength = 2;
    private readonly IInputAdapter _adapter;
    private readonly CandidateRanker _ranker;
    private readonly PredictionContextManager _contextManager;
    private bool _enabled = true;
    private bool _disposed;
    private IReadOnlyList<string> _currentCandidates = Array.Empty<string>();

    public DictionaryCompletionCoordinator(
        IInputAdapter adapter,
        CandidateRanker ranker,
        PredictionContextManager? contextManager = null)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(ranker);
        _adapter = adapter;
        _ranker = ranker;
        _contextManager = contextManager ?? new PredictionContextManager(
            InputContextSnapshot.MaximumTextBeforeCaret,
            InputContextSnapshot.MaximumTextAfterCaret);
        _adapter.ContextChanged += AdapterOnContextChanged;
        _adapter.SetEnabled(true);
    }

    public event EventHandler<CompletionUpdatedEventArgs>? SuggestionsChanged;

    public PredictionContext CurrentContext => _contextManager.Current;

    public void SetEnabled(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_enabled == enabled)
            return;

        _enabled = enabled;
        _currentCandidates = Array.Empty<string>();
        _adapter.SetCandidateSet(
            _contextManager.Current.Version,
            _contextManager.Current.FieldId,
            _currentCandidates);
        _adapter.SetEnabled(enabled);
        if (!enabled)
            PublishEmpty(_contextManager.Current.Version, _contextManager.Current.FieldId, null);
    }

    public void Dismiss()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        PredictionContext current = _contextManager.Current;
        _currentCandidates = Array.Empty<string>();
        _adapter.SetCandidateSet(current.Version, current.FieldId, _currentCandidates);
        PublishEmpty(current.Version, current.FieldId, null);
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
        if (!result.WasApplied && _contextManager.Current.Version == current.Version)
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
        try
        {
            if (!_contextManager.TryUpdate(snapshot))
                return;
        }
        catch (ArgumentException)
        {
            _contextManager.Invalidate(snapshot.Version);
            ClearAdapterCandidates(snapshot.Version, snapshot.FieldId);
            PublishEmpty(snapshot.Version, snapshot.FieldId, null);
            return;
        }

        if (!_enabled || !_contextManager.Current.CanPredictCompletion ||
            _contextManager.Current.Prefix.Length < MinimumPrefixLength)
        {
            ClearAdapterCandidates(snapshot.Version, snapshot.FieldId);
            PublishEmpty(snapshot.Version, snapshot.FieldId, snapshot.CaretBounds);
            return;
        }

        IReadOnlyList<string> candidates = _ranker.GetSuggestions(
                _contextManager.Current.Prefix,
                SuggestionState.MaximumSuggestions)
            .Select(entry => SuggestionFormatter.MatchPrefixCasing(
                _contextManager.Current.Prefix, entry.Word))
            .ToArray();

        _currentCandidates = candidates;
        _adapter.SetCandidateSet(snapshot.Version, snapshot.FieldId, candidates);
        SuggestionsChanged?.Invoke(this, new CompletionUpdatedEventArgs(
            snapshot.Version, snapshot.FieldId, candidates, snapshot.CaretBounds));
    }

    private void ClearAdapterCandidates(long version, string? fieldId)
    {
        _currentCandidates = Array.Empty<string>();
        _adapter.SetCandidateSet(version, fieldId, _currentCandidates);
    }

    private void PublishEmpty(long version, string? fieldId, CaretScreenBounds? caretBounds) =>
        SuggestionsChanged?.Invoke(this, new CompletionUpdatedEventArgs(
            version, fieldId, Array.Empty<string>(), caretBounds));
}
