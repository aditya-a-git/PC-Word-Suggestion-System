# PCWord manual verification guide

## Build and launch

From the repository root, with the .NET 10 SDK and Windows Desktop targeting pack installed:

    dotnet restore .\PCWord.sln
    dotnet test .\PCWord.sln --configuration Release
    dotnet run --project .\src\PCWord.Stage0\PCWord.Stage0.csproj

## Stage 0 controlled-editor checks

1. Put the caret at the start, middle, and end of a word. Confirm the reported current word spans the word while the caret offset remains the actual insertion point.
2. Select text and confirm the exact selection range is shown. Test Replace and verify the selected range is replaced once.
3. Put the caret in whitespace/punctuation and press Replace; record the actual behavior.
4. Insert [probe] repeatedly and verify no characters are lost or duplicated.
5. Test line breaks, tabs, apostrophes, underscores, digits, and non-ASCII letters. Record word-boundary limitations.
6. Move focus away and back; verify activation refreshes the snapshot.

## Stage 3 popup checks

1. Type **progr**, **comp**, or **hel**; confirm up to three demo candidates appear near the caret.
2. While the popup is visible, continue typing. Confirm keystrokes go into the editor and the candidates refresh.
3. Press Ctrl+Space and confirm the first candidate replaces the current word exactly once.
4. If at least two/three candidates are shown, test Ctrl+2 and Ctrl+3.
5. Press Escape. Confirm the popup hides and does not immediately reopen until the text/caret context changes.
6. Enable the sensitive-field checkbox. Confirm suggestions are hidden and acceptance does not edit the text.
7. Select text or move focus away. Confirm suggestions are hidden; no candidate should be accepted for a selected or stale context.
8. Place the caret near each edge of the editor and near the screen edges. Confirm the popup remains on-screen.
9. Confirm the popup does not take focus during ordinary typing. Record any OS/window-manager-specific behavior.
10. After accepting a candidate, inspect the exact text and caret position for duplicated or missing characters.

The demo vocabulary is intentionally hard-coded until Stage 4. These checks apply only to PCWord's own controlled editor, not Notepad, browsers, Office, VS Code, terminals, or other processes.

Record Windows version/build, .NET SDK version, and pass/fail/unavailable result without including real user text.
