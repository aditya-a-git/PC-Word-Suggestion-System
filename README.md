# PCWord — Stage 3 controlled-editor popup prototype

PCWord now includes a non-activating suggestion popup experiment inside the controlled WinForms editor. It displays up to three sample completions near the caret, supports deliberate keyboard acceptance and dismissal, and rejects acceptance when the text/caret snapshot is stale.

## Requirements

- Windows 10/11 x64 (intended Windows 11 target)
- .NET 10 SDK and Windows Desktop targeting pack

Run from the repository root:

    dotnet restore .\PCWord.sln
    dotnet test .\PCWord.sln --configuration Release
    dotnet run --project .\src\PCWord.Stage0\PCWord.Stage0.csproj

## Try the Stage 3 popup

1. Launch the controlled editor and click **Load sample text**, or type a word prefix such as **progr**, **comp**, or **hel**.
2. A small popup should appear close to the caret when the prefix matches the demo candidate list.
3. Press **Ctrl+Space** or **Ctrl+1** to accept the first candidate. **Ctrl+2** and **Ctrl+3** accept the second and third candidates when available.
4. Press **Escape** to dismiss suggestions for the current text/caret state. Typing or moving the caret allows the popup to refresh.
5. Enable **Treat this field as sensitive** and confirm that suggestions disappear.
6. Select text or move focus away; the popup should hide rather than apply an edit to an uncertain context.

## Scope and limitations

- The popup uses WinForms' non-activation styles and is positioned using caret geometry from the RichTextBox owned by this app. This does **not** prove correct caret geometry or focus behavior in other applications.
- Suggestions currently come from a tiny, hard-coded demo vocabulary. Stage 4 will connect the popup to the Stage 1 dictionary prediction engine.
- The popup is not a system-wide overlay. No TSF adapter, global keyboard hook, cross-process text editing, or universal compatibility is implemented.
- Acceptance rechecks the current text, selection and caret snapshot before replacing the current word in the controlled editor. This local proof does not replace adapter-level edit verification for a future system integration.
- Keyboard acceptance and Escape dismissal are implemented. Mouse selection is intentionally omitted until it can be tested without activation/focus disruption.
- No typed text is logged or persisted; the popup operates on transient in-memory editor state.

## Project notes

- Architecture decisions: docs/architecture-decisions.md
- Compatibility matrix: docs/compatibility-matrix.md
- Privacy model: docs/privacy-model.md
- Testing guide: docs/testing-guide.md
- Implementation plan: plan.md
