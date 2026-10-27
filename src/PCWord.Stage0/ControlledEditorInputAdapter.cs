using System.Runtime.InteropServices;
using System.Text;
using PCWord.Core.Input;

namespace PCWord.Stage0;

/// <summary>
/// Concrete IInputAdapter for the RichTextBox owned by PCWord. It reads only bounded
/// ranges from the control and edits only a validated current completion. It is not a
/// cross-process adapter.
/// </summary>
internal sealed class ControlledEditorInputAdapter : IInputAdapter, IDisposable
{
    private readonly RichTextBox _editor;
    private readonly Func<bool> _isSensitive;
    private readonly string _fieldId = $"pcword-editor-{Guid.NewGuid():N}";
    private readonly ImeMessageObserver _imeObserver;
    private InputContextSnapshot? _latestSnapshot;
    private string[] _currentCandidates = Array.Empty<string>();
    private long _candidateVersion = -1;
    private string? _candidateFieldId;
    private long _version;
    private bool _enabled = true;
    private bool _isComposing;
    private bool _applyingEdit;
    private bool _disposed;

    public ControlledEditorInputAdapter(RichTextBox editor, Func<bool> isSensitive)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(isSensitive);
        _editor = editor;
        _isSensitive = isSensitive;
        _imeObserver = new ImeMessageObserver(editor, OnCompositionChanged);

        _editor.TextChanged += EditorOnContextChanged;
        _editor.SelectionChanged += EditorOnContextChanged;
        _editor.GotFocus += EditorOnContextChanged;
        _editor.LostFocus += EditorOnContextChanged;
        _editor.HandleCreated += EditorOnHandleCreated;
        _editor.HandleDestroyed += EditorOnHandleDestroyed;
        _imeObserver.AttachIfCreated();
        PublishSnapshot();
    }

    public event EventHandler<InputContextChangedEventArgs>? ContextChanged;

    public InputContextSnapshot? LatestSnapshot => _latestSnapshot;

    public void SetEnabled(bool enabled)
    {
        ThrowIfDisposed();
        if (_enabled == enabled)
            return;

        _enabled = enabled;
        ClearCandidates();
        PublishSnapshot();
    }

    public void SetCandidateSet(long contextVersion, string? fieldId, IReadOnlyList<string> candidates)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(candidates);

        if (!_enabled || _latestSnapshot is null ||
            contextVersion != _latestSnapshot.Version ||
            !StringComparer.Ordinal.Equals(fieldId, _latestSnapshot.FieldId) ||
            candidates.Count > 3 || candidates.Any(candidate =>
                string.IsNullOrEmpty(candidate) || candidate.Length > 256))
        {
            ClearCandidates();
            return;
        }

        _candidateVersion = contextVersion;
        _candidateFieldId = fieldId;
        _currentCandidates = candidates.ToArray();
    }

    public ValueTask<TextEditResult> ApplyEditAsync(
        TextEditRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_disposed || _editor.IsDisposed)
            return ValueTask.FromResult(new TextEditResult(TextEditStatus.Unsupported));

        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromResult(new TextEditResult(TextEditStatus.Cancelled));

        if (!_editor.InvokeRequired)
            return ValueTask.FromResult(ApplyOnEditorThread(request, cancellationToken));

        if (!_editor.IsHandleCreated)
            return ValueTask.FromResult(new TextEditResult(TextEditStatus.Unsupported));

        var completion = new TaskCompletionSource<TextEditResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            _editor.BeginInvoke((Action)(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    completion.TrySetResult(new TextEditResult(TextEditStatus.Cancelled));
                    return;
                }
                completion.TrySetResult(ApplyOnEditorThread(request, cancellationToken));
            }));
        }
        catch (InvalidOperationException)
        {
            return ValueTask.FromResult(new TextEditResult(TextEditStatus.Unsupported));
        }

        return new ValueTask<TextEditResult>(completion.Task);
    }

    public void Refresh()
    {
        ThrowIfDisposed();
        PublishSnapshot();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _editor.TextChanged -= EditorOnContextChanged;
        _editor.SelectionChanged -= EditorOnContextChanged;
        _editor.GotFocus -= EditorOnContextChanged;
        _editor.LostFocus -= EditorOnContextChanged;
        _editor.HandleCreated -= EditorOnHandleCreated;
        _editor.HandleDestroyed -= EditorOnHandleDestroyed;
        _imeObserver.Dispose();
        ClearCandidates();
        _latestSnapshot = null;
        ContextChanged = null;
    }

    private TextEditResult ApplyOnEditorThread(TextEditRequest request, CancellationToken cancellationToken)
    {
        if (_disposed || _editor.IsDisposed || !_editor.IsHandleCreated)
            return new TextEditResult(TextEditStatus.Unsupported);
        if (cancellationToken.IsCancellationRequested)
            return new TextEditResult(TextEditStatus.Cancelled);

        InputContextSnapshot? previous = _latestSnapshot;
        InputContextSnapshot current = CreateSnapshot(_version);
        if (previous is null || !IsSameContext(previous, current))
        {
            PublishSnapshot();
            return new TextEditResult(TextEditStatus.StaleContext);
        }

        if (_candidateVersion != current.Version ||
            !StringComparer.Ordinal.Equals(_candidateFieldId, current.FieldId))
            return new TextEditResult(TextEditStatus.StaleContext);

        TextEditValidationResult validation = TextEditValidator.Validate(
            current, request, _version, _currentCandidates);
        if (!validation.IsValid)
            return new TextEditResult(validation.RefusalStatus ?? TextEditStatus.Failed);

        int caret = _editor.SelectionStart;
        int replacementStart = caret - request.ExpectedText.Length;
        if (replacementStart < 0)
            return new TextEditResult(TextEditStatus.StaleContext);

        _applyingEdit = true;
        try
        {
            _editor.Select(replacementStart, request.ExpectedText.Length);
            _editor.SelectedText = request.Replacement;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ExternalException or ArgumentOutOfRangeException)
        {
            return new TextEditResult(TextEditStatus.Failed);
        }
        finally
        {
            _applyingEdit = false;
        }

        bool verified = _editor.SelectionLength == 0 &&
            _editor.SelectionStart == replacementStart + request.Replacement.Length &&
            StringComparer.Ordinal.Equals(
                ReadRange(replacementStart, request.Replacement.Length), request.Replacement);
        if (!verified)
        {
            PublishSnapshot();
            return new TextEditResult(TextEditStatus.Failed);
        }

        ClearCandidates();
        PublishSnapshot();
        return new TextEditResult(TextEditStatus.Applied);
    }

    private void EditorOnContextChanged(object? sender, EventArgs args)
    {
        if (!_applyingEdit && !_disposed)
            PublishSnapshot();
    }

    private void EditorOnHandleCreated(object? sender, EventArgs args) => _imeObserver.AttachIfCreated();

    private void EditorOnHandleDestroyed(object? sender, EventArgs args) => _imeObserver.Detach();

    private void OnCompositionChanged(bool isComposing)
    {
        if (_disposed || _isComposing == isComposing)
            return;

        _isComposing = isComposing;
        PublishSnapshot();
    }

    private void PublishSnapshot()
    {
        if (_disposed)
            return;
        if (_version == long.MaxValue)
            throw new InvalidOperationException("The input context version is exhausted.");

        _version++;
        _latestSnapshot = CreateSnapshot(_version);
        if (!_latestSnapshot.ContextAvailable)
            ClearCandidates();
        ContextChanged?.Invoke(this, new InputContextChangedEventArgs(_latestSnapshot));
    }

    private InputContextSnapshot CreateSnapshot(long version)
    {
        bool focused = !_editor.IsDisposed && _editor.Focused;
        bool sensitive = _isSensitive();
        bool composing = _isComposing;
        int selectionLength = _editor.IsDisposed ? 0 : Math.Max(0, _editor.SelectionLength);
        bool available = _enabled && focused && !sensitive && !composing && selectionLength == 0 &&
            _editor.IsHandleCreated;

        string? before = null;
        string? after = null;
        CaretScreenBounds? caretBounds = null;
        int? caretOffset = null;
        if (available)
        {
            int caret = Math.Clamp(_editor.SelectionStart, 0, _editor.TextLength);
            before = ReadBoundedBefore(caret);
            after = ReadBoundedAfter(caret);
            caretBounds = GetCaretBounds(caret);
            caretOffset = caret;
            if (!IsWellFormedUtf16(before) || !IsWellFormedUtf16(after) ||
                before.Length > 0 && after.Length > 0 &&
                char.IsHighSurrogate(before[^1]) && char.IsLowSurrogate(after[0]))
            {
                before = null;
                after = null;
                caretBounds = null;
                caretOffset = null;
                available = false;
            }
        }

        return new InputContextSnapshot(
            version,
            _fieldId,
            before,
            after,
            selectionLength,
            focused,
            sensitive ? InputFieldSensitivity.Sensitive : InputFieldSensitivity.KnownSafe,
            composing,
            available,
            caretBounds,
            caretOffset);
    }

    private string ReadBoundedBefore(int caret)
    {
        int start = Math.Max(0, caret - (InputContextSnapshot.MaximumTextBeforeCaret + 1));
        string value = ReadRange(start, caret - start);
        if (value.Length <= InputContextSnapshot.MaximumTextBeforeCaret)
            return value;

        int remove = value.Length >= 2 && char.IsHighSurrogate(value[0]) && char.IsLowSurrogate(value[1]) ? 2 : 1;
        return value[remove..];
    }

    private string ReadBoundedAfter(int caret)
    {
        int count = Math.Min(
            _editor.TextLength - caret,
            InputContextSnapshot.MaximumTextAfterCaret + 1);
        string value = ReadRange(caret, count);
        if (value.Length <= InputContextSnapshot.MaximumTextAfterCaret)
            return value;

        int length = InputContextSnapshot.MaximumTextAfterCaret;
        if (length > 0 && char.IsHighSurrogate(value[length - 1]) && char.IsLowSurrogate(value[length]))
            length--;
        return value[..length];
    }

    private string ReadRange(int start, int length)
    {
        if (length <= 0 || !_editor.IsHandleCreated || _editor.IsDisposed)
            return string.Empty;

        int capacity = checked(length + 1);
        IntPtr textBuffer = Marshal.AllocHGlobal(checked(capacity * sizeof(char)));
        IntPtr rangeBuffer = Marshal.AllocHGlobal(Marshal.SizeOf<TextRangeNative>());
        try
        {
            var range = new TextRangeNative
            {
                Range = new CharacterRangeNative(start, checked(start + length)),
                Text = textBuffer
            };
            Marshal.StructureToPtr(range, rangeBuffer, false);
            IntPtr copiedPointer = SendMessageW(
                _editor.Handle, EmGetTextRange, IntPtr.Zero, rangeBuffer);
            int copied = (int)copiedPointer;
            if (copied <= 0)
                return string.Empty;

            int copiedLength = Math.Min(copied, length);
            var characters = new char[copiedLength];
            Marshal.Copy(textBuffer, characters, 0, copiedLength);
            return new string(characters);
        }
        finally
        {
            Marshal.FreeHGlobal(rangeBuffer);
            Marshal.FreeHGlobal(textBuffer);
        }
    }

    private CaretScreenBounds? GetCaretBounds(int caret)
    {
        try
        {
            Point client = _editor.GetPositionFromCharIndex(caret);
            if (client.X < 0 || client.Y < 0 || client.X > _editor.ClientSize.Width ||
                client.Y > _editor.ClientSize.Height)
                return null;

            Point screen = _editor.PointToScreen(client);
            int height = Math.Max(1, _editor.Font.Height);
            return new CaretScreenBounds(screen.X, screen.Y, screen.X + 1, screen.Y + height,
                Math.Max(1, _editor.DeviceDpi));
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static bool IsSameContext(InputContextSnapshot left, InputContextSnapshot right) =>
        StringComparer.Ordinal.Equals(left.FieldId, right.FieldId) &&
        StringComparer.Ordinal.Equals(left.TextBeforeCaret, right.TextBeforeCaret) &&
        StringComparer.Ordinal.Equals(left.TextAfterCaret, right.TextAfterCaret) &&
        left.SelectionLength == right.SelectionLength &&
        left.HasFocus == right.HasFocus &&
        left.Sensitivity == right.Sensitivity &&
        left.IsComposing == right.IsComposing &&
        left.ContextAvailable == right.ContextAvailable &&
        left.CaretOffset == right.CaretOffset &&
        left.CaretBounds == right.CaretBounds;

    private static bool IsWellFormedUtf16(string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index]))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                    return false;
                index++;
            }
            else if (char.IsLowSurrogate(value[index]))
            {
                return false;
            }
        }
        return true;
    }

    private void ClearCandidates()
    {
        _candidateVersion = -1;
        _candidateFieldId = null;
        _currentCandidates = Array.Empty<string>();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private const uint EmGetTextRange = 0x044B;
    private const int WmImeStartComposition = 0x010D;
    private const int WmImeEndComposition = 0x010E;

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CharacterRangeNative(int minimum, int maximum)
    {
        public readonly int Minimum = minimum;
        public readonly int Maximum = maximum;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TextRangeNative
    {
        public CharacterRangeNative Range;
        public IntPtr Text;
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    private sealed class ImeMessageObserver : NativeWindow, IDisposable
    {
        private readonly RichTextBox _editor;
        private readonly Action<bool> _compositionChanged;
        private bool _disposed;

        public ImeMessageObserver(RichTextBox editor, Action<bool> compositionChanged)
        {
            _editor = editor;
            _compositionChanged = compositionChanged;
        }

        public void AttachIfCreated()
        {
            if (!_disposed && _editor.IsHandleCreated && Handle != _editor.Handle)
            {
                Detach();
                AssignHandle(_editor.Handle);
            }
        }

        public void Detach()
        {
            if (Handle != IntPtr.Zero)
                ReleaseHandle();
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WmImeStartComposition)
                _compositionChanged(true);

            base.WndProc(ref message);

            if (message.Msg == WmImeEndComposition)
                _compositionChanged(false);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Detach();
        }
    }
}
