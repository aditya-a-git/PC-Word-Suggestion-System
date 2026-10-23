# PCWord — Stage 0

Stage 0 is a Windows controlled-editor feasibility harness plus a tested, transient text-context component. It is intentionally limited to its own WinForms editor. It does not install a text service, inspect other applications, predict words, or show a suggestion popup.

## Requirements

- .NET 8 SDK (the Windows editor targets `net8.0-windows`)
- Windows to run the WinForms editor

## Build, test, run

From this directory:

```powershell
dotnet build PCWord.sln
dotnet test PCWord.sln
dotnet run --project src/PCWord.Stage0/PCWord.Stage0.csproj
```

The editor reports its focus state, UTF-16 caret offset, selection, eligibility, current word, and up to 128 preceding UTF-16 units. “Apply explicit edit” replaces the selected text or (with no selection) the current word, preserving surrounding text. The sensitive-field checkbox disables context and edits. Use “Load sample text” to reproduce a controlled edit. The app does not record or transmit text.

## Stage 0 verification

Unit tests exercise the platform-independent context/edit model. The WinForms application is the controlled field. Cross-application, TSF, keyboard-hook, accessibility-provider and overlay behavior remain experiments; see `docs/compatibility-matrix.md` and `tools/feasibility/EXPERIMENTS.md`. Compatibility must be filled in only after following the procedures there on named app versions.
