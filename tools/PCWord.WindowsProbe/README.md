# PCWord Windows UI Automation feasibility probe

This is a **read-only feasibility tool**, not a production input adapter. It samples the focused UI Automation element and checks whether the provider exposes a text selection/caret range and bounded surrounding text.

## Run

From the repository root on Windows 11 with the .NET 10 SDK:

```powershell
dotnet run --project .\tools\PCWord.WindowsProbe\PCWord.WindowsProbe.csproj
```

Keep the probe console open, then focus a **non-sensitive test field** in Notepad or another application. The probe reports metadata such as process name, control type, whether UI Automation `TextPattern` is available, whether a selection exists, and how many preceding/following characters the provider returned.

Use only synthetic test text such as `PCWord probe sample`. Do not use passwords, private messages, source code, or real personal text.

## Privacy and safety behavior

- Does not install a keyboard hook or record keystrokes.
- Does not print, log, or persist text content. Surrounding text is held only transiently in memory to measure whether context is available.
- Skips password fields when UI Automation reports `IsPassword`.
- Skips surrounding context while a selection is present.
- Does not edit text, invoke `ValuePattern.SetValue`, send synthetic keystrokes, or modify the clipboard.
- Does not infer that a non-password field is safe. UI Automation does not provide a universal sensitivity guarantee.
- Provider failures and unavailable context are treated as unavailable, not guessed.

This probe does **not** prove that a production suggestion engine can safely replace text. UI Automation's TextPattern is read-only; an edit path needs a separate design and verification. The next architecture decision compares this limited read path with a native TSF text-service prototype.

## Manual test matrix

For each app, use synthetic text and record only the app/version, field type, capability booleans, and pass/fail. Never copy text content into test notes.

| Test | What to check |
|---|---|
| Notepad multiline edit | TextPattern, caret/selection, bounded context counts |
| Browser plain text input | Same capabilities in a local or blank page |
| Browser contenteditable | Whether the provider exposes a text range |
| VS Code editor | Provider behavior in the editor surface |
| Word or another office editor | Provider behavior in the document body |
| Terminal | Whether text is exposed at all; do not assume support |
| Password field | Password detected and context skipped |
| Selected text | Selection detected and context skipped |
| Focus moves between fields/apps | Reported process/control identity changes |

A successful read in one app does not imply support in another. The compatibility matrix must reflect only observed results.
