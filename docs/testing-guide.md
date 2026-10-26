# PCWord manual verification guide

## Build and launch

    dotnet restore .\PCWord.sln
    dotnet test .\PCWord.sln --configuration Release
    dotnet run --project .\src\PCWord.Stage0\PCWord.Stage0.csproj

## Stage 0 controlled-editor checks

1. Put the caret at the start, middle, and end of a word. Confirm the current word and caret offset are reported correctly.
2. Select text and test Replace; verify the selected range is replaced once.
3. Test punctuation, whitespace, line breaks, tabs, apostrophes, underscores, digits, and non-ASCII letters.
4. Move focus away and back; verify the context refreshes.

## Stage 3 popup checks

1. Type **p** or **c**. Confirm the suggestion popup does not appear for a one-character prefix.
2. Continue to **pro**, **com**, or **hel**. Confirm a compact suggestion panel appears inside the editor window.
3. Continue typing while it is visible. Confirm keystrokes still go into the editor and suggestions refresh.
4. Press Ctrl+Space to accept the first candidate; test Ctrl+2 and Ctrl+3 when those candidates are present.
5. Press Escape. Confirm suggestions hide and do not reopen until the text/caret context changes.
6. Enable sensitive mode. Confirm suggestions hide and no suggestion can be accepted.
7. Select text or move focus away. Confirm suggestions hide and stale context cannot be accepted.
8. Place the caret near the bottom/right edges of the editor. Confirm the panel stays inside the editor host.
9. Verify exact resulting text and caret after accepting a suggestion. Watch for duplicate or missing characters.

The demo vocabulary is hard-coded until Stage 4. These tests apply only to PCWord's own controlled editor, not Notepad, browsers, Office, VS Code, terminals, or other processes. Record pass/fail and Windows/.NET versions without including real user text.
