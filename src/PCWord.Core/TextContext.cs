using System.Text;

namespace PCWord.Core;

/// <summary>A transient snapshot from an explicitly controlled text field. Offsets use .NET UTF-16 indices.</summary>
public sealed record TextContext(string Text, int SelectionStart, int SelectionLength, int Caret, bool HasFocus, bool IsSensitive = false)
{
    public int SelectionEnd => checked(SelectionStart + SelectionLength);
    public bool HasSelection => SelectionLength > 0;
    public bool IsEligible => HasFocus && !IsSensitive && SelectionLength == 0 && Caret >= 0 && Caret <= Text.Length;

    public static TextContext Capture(string text, int selectionStart, int selectionLength, bool hasFocus, bool isSensitive = false, int maximumPrecedingContext = 128)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (selectionStart < 0 || selectionLength < 0 || selectionStart > text.Length || selectionLength > text.Length - selectionStart)
            throw new ArgumentOutOfRangeException(nameof(selectionStart), "Selection must be within the text.");
        if (maximumPrecedingContext < 0) throw new ArgumentOutOfRangeException(nameof(maximumPrecedingContext));
        // A selected range has no single insertion caret; SelectionStart is retained as the anchor.
        return new TextContext(text, selectionStart, selectionLength, selectionStart, hasFocus, isSensitive);
    }

    public string CurrentWord
    {
        get
        {
            if (!IsEligible) return string.Empty;
            int start = Caret;
            while (start > 0)
            {
                int previous = PreviousRuneStart(Text, start);
                if (!IsWordRune(ReadRune(Text, previous, start - previous))) break;
                start = previous;
            }
            int end = Caret;
            while (end < Text.Length)
            {
                int length = RuneLengthAt(Text, end);
                if (!IsWordRune(ReadRune(Text, end, length))) break;
                end += length;
            }
            return Text[start..Caret];
        }
    }

    public string PrecedingContext(int maximumCharacters = 128)
    {
        if (maximumCharacters < 0) throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
        if (!IsEligible || maximumCharacters == 0) return string.Empty;
        int start = Math.Max(0, Caret - maximumCharacters);
        if (start > 0 && start < Text.Length && char.IsLowSurrogate(Text[start]) && char.IsHighSurrogate(Text[start - 1])) start++;
        return Text[start..Caret];
    }

    /// <summary>Replaces the current selection (or word when there is no selection) with an explicit accepted edit.</summary>
    public (string Text, int Caret) ApplyExplicitEdit(string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        if (!HasFocus || IsSensitive || Caret < 0 || Caret > Text.Length) throw new InvalidOperationException("The field is not eligible for editing.");
        int start = Caret, length = 0;
        if (HasSelection) { start = SelectionStart; length = SelectionLength; }
        else
        {
            start = Caret;
            while (start > 0)
            {
                int previous = PreviousRuneStart(Text, start);
                if (!IsWordRune(ReadRune(Text, previous, start - previous))) break;
                start = previous;
            }
            length = Caret - start;
        }
        string updated = string.Concat(Text.AsSpan(0, start), replacement, Text.AsSpan(start + length));
        return (updated, start + replacement.Length);
    }

    private static int PreviousRuneStart(string text, int index) => index >= 2 && char.IsLowSurrogate(text[index - 1]) && char.IsHighSurrogate(text[index - 2]) ? index - 2 : index - 1;
    private static int RuneLengthAt(string text, int index) => char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;
    private static Rune ReadRune(string text, int index, int length) => length == 2 ? new Rune(text[index], text[index + 1]) : new Rune(text[index]);
    private static bool IsWordRune(Rune rune) => Rune.IsLetterOrDigit(rune) || rune.Value == '_';
}
