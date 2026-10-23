# PCWord — Stage 0.1 controlled editor

This is the first runnable feasibility harness for the Windows predictive typing assistant. It exercises a controlled WinForms `RichTextBox`: bounded context inspection, current-word/selection reporting, and explicit replacement/insertion. It is deliberately not a system-wide typing assistant.

## Requirements

- Windows 10/11 x64 (intended Windows 11 target)
- .NET 10 SDK and Windows Desktop targeting pack

The current environment has .NET runtimes but **no .NET SDK**, so the project has not been compiled or run here. Install the .NET 10 SDK from Microsoft's .NET download page, then run from this directory:

```powershell
dotnet run --project .\Stage0\PCWord.Stage0.csproj
```

Or publish a self-contained folder:

```powershell
dotnet publish .\Stage0\PCWord.Stage0.csproj -c Release -r win-x64 --self-contained true
```

## Try the harness

1. Type a few sentences into the top editor.
2. Move the caret into a word, place it within a word, or select a range.
3. Observe the lower panel's caret UTF-16 offset, selection, word around the caret, and up to 80 preceding / 40 following characters.
4. Use the explicit replace/insert buttons and inspect the resulting text and caret.
5. Try an empty position, punctuation, Unicode letters, a selection spanning text, and an edit at the start/end of the buffer.

## Scope and limits

- The app reads and edits only its own `RichTextBox`, using WinForms text and selection properties. That makes the harness controlled and deterministic; it does **not** establish TSF, UI Automation, keyboard hook, caret geometry, or cross-process behavior.
- Caret offsets are .NET UTF-16 string offsets. The displayed `Focused control` is a simple app-level diagnostic, not a Windows focus identity API.
- The current-word expansion uses letters, digits, underscore, and apostrophe. This is an experiment, not a complete Unicode word-boundary implementation.
- No prediction engine, global hook, popup, startup integration, persistence, telemetry, or raw-key logging is implemented.
- Buttons perform direct edits inside the controlled editor only. No external application can be changed by this app.

## Stage 0 documentation

- [Architecture decisions](docs/architecture-decisions.md)
- [Compatibility matrix](docs/compatibility-matrix.md)
- [Privacy model](docs/privacy-model.md)
- [Testing guide](docs/testing-guide.md)
- [Experiments and next steps](tools/feasibility/README.md)

The supplied planning references remain under `sources/` and are treated as read-only.
