namespace PCWord.WindowsUI.Logic;

public sealed class SuggestionPopupState
{
    public const int MaximumSuggestions = 3;
    private long _version = -1;
    private string? _fieldId;
    private string[] _candidates = [];

    public long ContextVersion => _version;
    public string? FieldId => _fieldId;
    public IReadOnlyList<string> Candidates => _candidates;
    public int SelectedIndex { get; private set; }
    public bool IsVisible => _candidates.Length > 0 && _fieldId is not null;

    public bool TryUpdate(long version, string? fieldId, IEnumerable<string> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (version < 0 || version <= _version)
            return false;
        if (fieldId?.Length > PCWord.Core.Input.InputContextSnapshot.MaximumFieldIdLength)
            return false;

        _version = version;
        _fieldId = string.IsNullOrWhiteSpace(fieldId) ? null : fieldId;
        _candidates = _fieldId is null
            ? []
            : candidates
                .Where(value => !string.IsNullOrWhiteSpace(value) && value.Length <= 256)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaximumSuggestions)
                .ToArray();
        SelectedIndex = 0;
        return true;
    }

    public bool IsCurrent(long version, string? fieldId) =>
        IsVisible && version == _version &&
        StringComparer.Ordinal.Equals(fieldId, _fieldId);

    public bool MoveSelection(int delta)
    {
        if (!IsVisible || delta == 0) return false;
        int count = _candidates.Length;
        SelectedIndex = ((SelectedIndex + delta) % count + count) % count;
        return true;
    }

    public bool TryGetSelected(out string candidate)
    {
        if (IsVisible && SelectedIndex >= 0 && SelectedIndex < _candidates.Length)
        {
            candidate = _candidates[SelectedIndex];
            return true;
        }

        candidate = string.Empty;
        return false;
    }

    public bool Dismiss(long version)
    {
        if (version < _version) return false;
        _version = version;
        _fieldId = null;
        _candidates = [];
        SelectedIndex = 0;
        return true;
    }
}
