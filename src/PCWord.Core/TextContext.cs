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

            var (start, length) = GetWordSpan(Caret);
            return length == 0 ? string.Empty : Text.Substring(start, length);
        }
    }

    public string TypedPrefix
    {
        get
        {
            if (!IsEligible) return string.Empty;

            var (start, length) = GetWordSpan(Caret);
            if (length == 0) return string.Empty;

            int prefixEnd = Math.Clamp(Caret, start, start + length);
            return Text[start..prefixEnd];
        }
    }

    private (int Start, int Length) GetWordSpan(int caret)
    {
        if (caret < 0 || caret > Text.Length)
            return (caret, 0);

        int start;

        // Prefer the word to the left when the caret is at its end
        // or somewhere inside it.
        if (caret > 0)
        {
            int previous = PreviousRuneStart(Text, caret);
            if (IsWordRune(ReadRune(Text, previous, caret - previous)))
            {
                start = previous;

                while (start > 0)
                {
                    previous = PreviousRuneStart(Text, start);
                    if (!IsWordRune(ReadRune(Text, previous, start - previous)))
                        break;

                    start = previous;
                }

                int end = caret;
                while (end < Text.Length)
                {
                    int runeLength = RuneLengthAt(Text, end);
                    if (!IsWordRune(ReadRune(Text, end, runeLength)))
                        break;

                    end += runeLength;
                }

                return (start, end - start);
            }
        }

        // At the beginning of a word, find the word to the right.
        if (caret < Text.Length)
        {
            int runeLength = RuneLengthAt(Text, caret);
            if (IsWordRune(ReadRune(Text, caret, runeLength)))
            {
                int end = caret;

                while (end < Text.Length)
                {
                    runeLength = RuneLengthAt(Text, end);
                    if (!IsWordRune(ReadRune(Text, end, runeLength)))
                        break;

                    end += runeLength;
                }

                return (caret, end - caret);
            }
        }

        return (caret, 0);
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

        if (!HasFocus || IsSensitive || Caret < 0 || Caret > Text.Length)
            throw new InvalidOperationException(
                "The field is not eligible for editing.");

        int start;
        int length;

        if (HasSelection)
        {
            start = SelectionStart;
            length = SelectionLength;
        }
        else
        {
            (start, length) = GetWordSpan(Caret);
        }

        string updated = string.Concat(
            Text.AsSpan(0, start),
            replacement,
            Text.AsSpan(start + length));

        return (updated, start + replacement.Length);
    }

    private static int PreviousRuneStart(string text, int index) => index >= 2 && char.IsLowSurrogate(text[index - 1]) && char.IsHighSurrogate(text[index - 2]) ? index - 2 : index - 1;
    private static int RuneLengthAt(string text, int index) => char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;
    private static Rune ReadRune(string text, int index, int length) => length == 2 ? new Rune(text[index], text[index + 1]) : new Rune(text[index]);
    private static bool IsWordRune(Rune rune) => Rune.IsLetterOrDigit(rune) || rune.Value == '_';
}
