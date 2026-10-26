# PCWord — Stage 3 controlled-editor suggestion prototype

PCWord includes a compact suggestion surface inside the Stage 0 controlled editor. It displays up to three sample completions near the caret, supports deliberate keyboard acceptance and dismissal, and checks the text/caret snapshot before editing.

## Run

Requirements: Windows and the .NET 10 SDK with the Windows Desktop targeting pack.

    dotnet restore .\PCWord.sln
    dotnet test .\PCWord.sln --configuration Release
    dotnet run --project .\src\PCWord.Stage0\PCWord.Stage0.csproj

## Try it

1. Type a prefix such as **progr**, **comp**, or **hel**. The popup starts after two or more prefix characters to avoid noisy single-letter results.
2. The suggestions appear as a compact panel inside the editor window, not as a separate top-level window.
3. Press **Ctrl+Space** or **Ctrl+1** to accept the first candidate; **Ctrl+2** or **Ctrl+3** accepts the second or third candidate when available.
4. Press **Escape** to dismiss suggestions for the current text/caret state.
5. Continue typing while the popup is visible; the editor should retain focus.
6. Enable **Treat this field as sensitive** and confirm that suggestions disappear.

## Scope and limitations

- The popup is a child control inside PCWord's own editor window. This avoids activating a separate top-level popup window, but is not a system-wide overlay.
- Caret geometry comes from the RichTextBox owned by this app. No cross-process caret acquisition or editing is implemented.
- Suggestions use a tiny hard-coded demo vocabulary. Stage 4 will connect the actual dictionary prediction engine.
- Acceptance checks the current editor text, selection and caret snapshot before a local replacement. This does not implement the Stage 2 cross-process IInputAdapter.
- No typed text is logged or persisted. No TSF adapter, global keyboard hook, or universal compatibility is claimed.

See docs/testing-guide.md, docs/compatibility-matrix.md, docs/privacy-model.md, and docs/architecture-decisions.md.
