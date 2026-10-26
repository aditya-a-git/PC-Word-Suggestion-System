using System.Text;
using PCWord.Core.Input;

namespace PCWord.Prediction.Context;

public enum PredictionContextStatus
{
    Unavailable = 0,
    Ineligible = 1,
    PartialWord = 2,
    WordBoundary = 3
}

/// <summary>
/// A short-lived, bounded context prepared for prediction. Text is held in memory only
/// and is never logged or persisted by this component.
/// </summary>
public sealed record PredictionContext(
    long Version,
    string? FieldId,
    PredictionContextStatus Status,
    string Prefix,
    string PrecedingText,
    string FollowingText)
{
    public bool CanPredictCompletion => Status == PredictionContextStatus.PartialWord && Prefix.Length > 0;
    public bool CanPredictNextWord => Status == PredictionContextStatus.WordBoundary;
}

public sealed class PredictionContextManager
{
    private readonly int _maximumPrecedingCharacters;
    private readonly int _maximumFollowingCharacters;
    private long _lastVersion = -1;
    private string? _lastFieldId;
    private PredictionContext _current = Empty(-1);

    public PredictionContextManager(int maximumPrecedingCharacters = 128, int maximumFollowingCharacters = 32)
    {
        if (maximumPrecedingCharacters < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumPrecedingCharacters));
        if (maximumFollowingCharacters < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumFollowingCharacters));

        _maximumPrecedingCharacters = maximumPrecedingCharacters;
        _maximumFollowingCharacters = maximumFollowingCharacters;
    }

    public PredictionContext Current => _current;

    /// <summary>
    /// Applies a newer snapshot. Older/equal versions are rejected so delayed events cannot
    /// roll context back. A field change invalidates old context before processing the new one.
    /// </summary>
    public bool TryUpdate(InputContextSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Version <= _lastVersion)
            return false;

        _lastVersion = snapshot.Version;

        bool fieldChanged = !StringComparer.Ordinal.Equals(_lastFieldId, snapshot.FieldId);
        _lastFieldId = snapshot.FieldId;

        if (fieldChanged)
            _current = Empty(snapshot.Version);

        if (!snapshot.ContextAvailable)
        {
            _current = new PredictionContext(snapshot.Version, snapshot.FieldId,
                PredictionContextStatus.Unavailable, string.Empty, string.Empty, string.Empty);
            return true;
        }

        if (snapshot.SelectionLength < 0)
            throw new ArgumentOutOfRangeException(nameof(snapshot), "Selection length cannot be negative.");

        if (!snapshot.HasFocus ||
            string.IsNullOrWhiteSpace(snapshot.FieldId) ||
            snapshot.Sensitivity != InputFieldSensitivity.KnownSafe ||
            snapshot.SelectionLength != 0 ||
            snapshot.IsComposing ||
            snapshot.TextBeforeCaret is null ||
            snapshot.TextAfterCaret is null)
        {
            _current = new PredictionContext(snapshot.Version, snapshot.FieldId,
                PredictionContextStatus.Ineligible, string.Empty, string.Empty, string.Empty);
            return true;
        }

        string preceding = TrimStartWithoutSplittingSurrogate(
            snapshot.TextBeforeCaret, _maximumPrecedingCharacters);
        string following = TrimEndWithoutSplittingSurrogate(
            snapshot.TextAfterCaret, _maximumFollowingCharacters);

        string prefix = GetTrailingWord(preceding);
        PredictionContextStatus status = prefix.Length > 0
            ? PredictionContextStatus.PartialWord
            : PredictionContextStatus.WordBoundary;

        _current = new PredictionContext(snapshot.Version, snapshot.FieldId, status,
            prefix, preceding, following);
        return true;
    }

    /// <summary>Explicitly invalidates current context, for adapter shutdown or lost focus.</summary>
    public void Invalidate(long version)
    {
        if (version <= _lastVersion)
            return;

        _lastVersion = version;
        _current = Empty(version);
        _lastFieldId = null;
    }

    private static PredictionContext Empty(long version) =>
        new(version, null, PredictionContextStatus.Unavailable, string.Empty, string.Empty, string.Empty);

    private static string GetTrailingWord(string text)
    {
        if (text.Length == 0)
            return string.Empty;

        int start = text.Length;
        int index = text.Length;
        while (index > 0)
        {
            int runeStart = PreviousRuneStart(text, index);
            Rune rune = Rune.GetRuneAt(text, runeStart);
            if (!IsWordRune(rune))
                break;

            start = runeStart;
            index = runeStart;
        }

        return text[start..];
    }

    private static bool IsWordRune(Rune rune) => Rune.IsLetterOrDigit(rune) || rune.Value == '_';

    private static int PreviousRuneStart(string text, int index) =>
        index >= 2 && char.IsLowSurrogate(text[index - 1]) && char.IsHighSurrogate(text[index - 2])
            ? index - 2
            : index - 1;

    private static string TrimStartWithoutSplittingSurrogate(string value, int maximum)
    {
        if (value.Length <= maximum)
            return value;

        int start = value.Length - maximum;
        if (start > 0 && start < value.Length &&
            char.IsLowSurrogate(value[start]) && char.IsHighSurrogate(value[start - 1]))
            start++;

        return value[start..];
    }

    private static string TrimEndWithoutSplittingSurrogate(string value, int maximum)
    {
        if (value.Length <= maximum)
            return value;

        int end = maximum;
        if (end > 0 && end < value.Length &&
            char.IsHighSurrogate(value[end - 1]) && char.IsLowSurrogate(value[end]))
            end--;

        return value[..end];
    }
}
