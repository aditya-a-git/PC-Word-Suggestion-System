namespace PCWord.Core.Suggestions;

/// <summary>
/// Holds a bounded, versioned set of suggestions for one input field. This type has no
/// UI or platform dependencies and rejects results that arrive out of order.
/// </summary>
public sealed class SuggestionState
{
    public const int MaximumSuggestions = 3;

    private long _contextVersion = -1;
    private string? _fieldId;
    private IReadOnlyList<string> _suggestions = Array.Empty<string>();

    public long ContextVersion => _contextVersion;
    public string? FieldId => _fieldId;
    public IReadOnlyList<string> Suggestions => _suggestions;
    public bool IsVisible => _suggestions.Count > 0;

    /// <summary>
    /// Publishes suggestions for a newer context. Empty/invalid fields clear the popup.
    /// Candidates are trimmed only for validation, deduplicated case-insensitively, and capped at three.
    /// </summary>
    public bool TryUpdate(long contextVersion, string? fieldId, IEnumerable<string> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (contextVersion <= _contextVersion)
            return false;

        _contextVersion = contextVersion;
        _fieldId = string.IsNullOrWhiteSpace(fieldId) ? null : fieldId;

        if (_fieldId is null)
        {
            _suggestions = Array.Empty<string>();
            return true;
        }

        _suggestions = candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaximumSuggestions)
            .ToArray();

        return true;
    }

    /// <summary>Clears suggestions for this or a newer context without accepting an edit.</summary>
    public bool Dismiss(long contextVersion)
    {
        if (contextVersion < _contextVersion)
            return false;

        _contextVersion = contextVersion;
        _suggestions = Array.Empty<string>();
        return true;
    }

    /// <summary>
    /// Returns a candidate only if the request still refers to the exact current context and field.
    /// </summary>
    public bool TryAccept(long contextVersion, string fieldId, int candidateIndex, out string candidate)
    {
        candidate = string.Empty;

        if (!IsVisible ||
            contextVersion != _contextVersion ||
            !StringComparer.Ordinal.Equals(fieldId, _fieldId) ||
            candidateIndex < 0 ||
            candidateIndex >= _suggestions.Count)
        {
            return false;
        }

        candidate = _suggestions[candidateIndex];
        return true;
    }
}
