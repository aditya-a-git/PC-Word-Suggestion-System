using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using PCWord.Core.Input;
using PCWord.Prediction.Context;
using PCWord.WindowsUI.Logic;

namespace PCWord.WindowsUI;

/// <summary>
/// Bridges current coordinator results to a non-activating popup. Input events are
/// delivered by the coordinator; acceptance is always delegated back to its adapter.
/// </summary>
public sealed class SuggestionPopupController : IDisposable
{
    private readonly DictionaryCompletionCoordinator _coordinator;
    private readonly SuggestionPopupWindow _window;
    private readonly SuggestionPopupState _state = new();
    private Rectangle? _lastCaret;
    private int _accepting;
    private bool _disposed;

    public SuggestionPopupController(DictionaryCompletionCoordinator coordinator,
        SuggestionPopupWindow? window = null)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        _coordinator = coordinator;
        _window = window ?? new SuggestionPopupWindow();
        _window.CandidateClicked += OnCandidateClicked;
        _coordinator.SuggestionsChanged += OnSuggestionsChanged;
    }

    public SuggestionPopupWindow Window => _window;

    /// <summary>Called by a future approved input-key route; the popup installs no hook.</summary>
    public bool MoveSelection(int delta)
    {
        if (_disposed || _state.MoveSelection(delta) == false) return false;
        Render();
        return true;
    }

    public async ValueTask<TextEditResult> AcceptSelectedAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Interlocked.CompareExchange(ref _accepting, 1, 0) != 0)
            return new TextEditResult(TextEditStatus.Unsupported);

        try
        {
            if (!_state.TryGetSelected(out _) ||
                !_coordinator.IsCurrent(_state.ContextVersion, _state.FieldId))
            {
                _window.Hide();
                return new TextEditResult(TextEditStatus.StaleContext);
            }

            TextEditResult result = await _coordinator.AcceptAsync(_state.SelectedIndex, cancellationToken);
            _window.Hide();
            return result;
        }
        finally
        {
            Interlocked.Exchange(ref _accepting, 0);
        }
    }

    public void Dismiss()
    {
        if (_disposed) return;
        _state.Dismiss(Math.Max(_state.ContextVersion, _coordinator.CurrentContext.Version));
        _lastCaret = null;
        _window.Hide();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _coordinator.SuggestionsChanged -= OnSuggestionsChanged;
        _window.CandidateClicked -= OnCandidateClicked;
        _window.Hide();
        _window.Dispose();
    }

    private void OnSuggestionsChanged(object? sender, CompletionUpdatedEventArgs update)
    {
        if (_disposed) return;
        if (_window.InvokeRequired)
        {
            try { _window.BeginInvoke(new Action(() => ApplyUpdate(update))); }
            catch (InvalidOperationException) { }
            return;
        }
        ApplyUpdate(update);
    }

    private void ApplyUpdate(CompletionUpdatedEventArgs update)
    {
        if (_disposed || !_coordinator.IsCurrent(update.ContextVersion, update.FieldId) ||
            update.Candidates.Count == 0 || update.CaretBounds is null)
        {
            _state.Dismiss(Math.Max(_state.ContextVersion, update.ContextVersion));
            _lastCaret = null;
            _window.Hide();
            return;
        }

        if (!_state.TryUpdate(update.ContextVersion, update.FieldId, update.Candidates))
        {
            if (!_state.IsCurrent(update.ContextVersion, update.FieldId))
                _window.Hide();
            return;
        }

        CaretScreenBounds bounds = update.CaretBounds.Value;
        _lastCaret = Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
        Render();
    }

    private void Render()
    {
        PredictionContext context = _coordinator.CurrentContext;
        if (!_state.IsCurrent(context.Version, context.FieldId) ||
            !_coordinator.IsCurrent(context.Version, context.FieldId) ||
            _lastCaret is not { } caret)
        {
            _window.Hide();
            return;
        }

        _window.UpdateSuggestions(_state.Candidates, _state.SelectedIndex, context.CanPredictNextWord);
        _window.ShowAtCaret(caret);
    }

    private void OnCandidateClicked(int index)
    {
        if (_disposed || Volatile.Read(ref _accepting) != 0 ||
            !_state.IsCurrent(_state.ContextVersion, _coordinator.CurrentContext.FieldId))
            return;
        while (_state.SelectedIndex != index && _state.MoveSelection(1)) { }
        Render();
        _ = AcceptSelectedAsync();
    }
}
