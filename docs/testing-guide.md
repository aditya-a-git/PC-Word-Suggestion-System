# Stage 0.1 manual verification

## Build and launch

From repository root, with the .NET 10 SDK and Windows Desktop targeting pack installed:

```powershell
dotnet run --project .\Stage0\PCWord.Stage0.csproj
```

## Scenarios

1. Put the caret at the start, middle, and end of a word. Confirm the reported current word spans the word while the caret offset remains the actual insertion point.
2. Select text and confirm the exact selection range is shown. Test Replace and verify the selected range is replaced once.
3. Put the caret in whitespace/punctuation and press Replace; it should report that no word was replaced.
4. Insert `[probe]` repeatedly and verify no characters are lost or duplicated.
5. Test line breaks, tabs, apostrophes, underscores, digits, and non-ASCII letters. Record word-boundary limitations.
6. Move focus away and back; verify activation refreshes the snapshot.

Record Windows version/build, .NET SDK version, and observed result in a local test report without including real user text. Automated tests are not included yet.
