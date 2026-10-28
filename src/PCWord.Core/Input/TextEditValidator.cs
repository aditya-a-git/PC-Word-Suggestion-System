using System.Globalization;
using System.Text;

namespace PCWord.Core.Input;

/// <summary>Result of validating an explicit edit against the latest known snapshot.</summary>
public sealed record TextEditValidationResult(bool IsValid, TextEditStatus? RefusalStatus = null)
{
    public static TextEditValidationResult Valid { get; } = new(true);
    public static TextEditValidationResult Refused(TextEditStatus status) => new(false, status);
}

/// <summary>
/// Shared preflight for platform adapters. It validates freshness and candidate provenance;
/// it does not perform an edit or claim that a platform backend can verify one.
/// </summary>
public static class TextEditValidator
{
    public static TextEditValidationResult Validate(
        InputContextSnapshot snapshot,
        TextEditRequest request,
        long currentVersion,
        IReadOnlyCollection<string> currentCandidates)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(currentCandidates);

        if (snapshot.Sensitivity == InputFieldSensitivity.Sensitive)
            return TextEditValidationResult.Refused(TextEditStatus.RefusedSensitiveField);

        if (snapshot.Version != currentVersion || request.ContextVersion != currentVersion ||
            !StringComparer.Ordinal.Equals(snapshot.FieldId, request.FieldId))
            return TextEditValidationResult.Refused(TextEditStatus.StaleContext);

        if (!snapshot.ContextAvailable || snapshot.Sensitivity != InputFieldSensitivity.KnownSafe ||
            !snapshot.HasFocus || string.IsNullOrWhiteSpace(snapshot.FieldId) ||
            snapshot.TextBeforeCaret is null || snapshot.TextAfterCaret is null ||
            snapshot.SelectionLength < 0)
            return TextEditValidationResult.Refused(TextEditStatus.Unsupported);

        if (snapshot.SelectionLength != 0 || snapshot.IsComposing)
            return TextEditValidationResult.Refused(TextEditStatus.Unsupported);

        if (request.ExpectedText is null || request.Replacement is null ||
            request.ExpectedText.Length > InputContextSnapshot.MaximumTextBeforeCaret ||
            request.Replacement.Length > 256)
            return TextEditValidationResult.Refused(TextEditStatus.Unsupported);

        string expectedPrefix;
        try
        {
            expectedPrefix = GetTrailingWord(snapshot.TextBeforeCaret);
            if (snapshot.TextBeforeCaret.Length == InputContextSnapshot.MaximumTextBeforeCaret &&
                expectedPrefix.Length == snapshot.TextBeforeCaret.Length &&
                IsWordRune(Rune.GetRuneAt(snapshot.TextBeforeCaret, 0)))
                return TextEditValidationResult.Refused(TextEditStatus.Unsupported);
            if (HasWordContinuationAtCaret(snapshot.TextAfterCaret))
                return TextEditValidationResult.Refused(TextEditStatus.Unsupported);
        }
        catch (ArgumentException)
        {
            // Malformed UTF-16 from a host is unavailable context, never an edit target.
            return TextEditValidationResult.Refused(TextEditStatus.Unsupported);
        }

        if (!StringComparer.Ordinal.Equals(expectedPrefix, request.ExpectedText))
            return TextEditValidationResult.Refused(TextEditStatus.StaleContext);

        bool candidateIsCurrent = currentCandidates.Contains(request.Replacement, StringComparer.Ordinal);
        if (!candidateIsCurrent || request.Replacement.Length == 0 ||
            (expectedPrefix.Length > 0 && (request.Replacement.Length <= expectedPrefix.Length ||
            !request.Replacement.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))))
            return TextEditValidationResult.Refused(TextEditStatus.Failed);

        return TextEditValidationResult.Valid;
    }

    private static string GetTrailingWord(string text)
    {
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

    private static bool HasWordContinuationAtCaret(string textAfterCaret) =>
        textAfterCaret.Length > 0 && IsWordRune(Rune.GetRuneAt(textAfterCaret, 0));

    private static bool IsWordRune(Rune rune)
    {
        UnicodeCategory category = Rune.GetUnicodeCategory(rune);
        return Rune.IsLetterOrDigit(rune) || rune.Value is '_' or '\'' or 0x2019 ||
            category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;
    }

    private static int PreviousRuneStart(string text, int index) =>
        index >= 2 && char.IsLowSurrogate(text[index - 1]) && char.IsHighSurrogate(text[index - 2])
            ? index - 2
            : index - 1;
}
