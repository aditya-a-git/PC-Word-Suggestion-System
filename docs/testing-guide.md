# Testing guide

## Managed tests

```powershell
dotnet build .\PCWord.sln --configuration Release
dotnet test .\PCWord.sln --configuration Release
```

Core tests cover context parsing, prediction limits/staleness, and explicit-edit validation. Run UI behavior manually in the controlled editor with synthetic text. Confirm the popup remains in the owned window, retains editor focus, accepts only a current candidate, rejects sensitive/selected/mid-word contexts, and produces the exact expected text and caret.

## Native policy tests and build

```powershell
cmake -S .\tools\PCWord.TsfProbe -B .\tools\PCWord.TsfProbe\build -A x64
cmake --build .\tools\PCWord.TsfProbe\build --config Release
ctest --test-dir .\tools\PCWord.TsfProbe\build -C Release --output-on-failure
```

The two CTest executables are unit tests only; they do not activate TSF or test an application provider. If `build/` contains user-local tracked output, configure a fresh ignored directory such as `build-validation` instead.

## Integration tests

The UI Automation and TSF probes need an interactive Windows host. Use only disposable synthetic text. Start with reads disabled; for TSF, the environment opt-in is necessary but scope metadata must still pass the fail-closed policy. Do not register the TSF profile on a daily-use account; do not elevate or use machine-wide registration. Close apps and unregister when finished. Record app/version and capability flags—not text—in `docs/compatibility-matrix.md`.

No external application runtime test is claimed until it is performed. Current exact build/test results and open blockers are in `docs/windows-integration-status.md`.

## Windows integration update (2026-10-27)

### Managed build and unit tests

Run from the repository root:

```powershell
dotnet restore .\PCWord.sln
dotnet build .\PCWord.sln --configuration Release --no-restore
dotnet test .\PCWord.sln --configuration Release --no-restore --verbosity minimal
```

The current local test run passed 120/120 tests. This verifies deterministic contracts and fakes, not interactive WinForms behavior or cross-process integration.

### Native TSF policy build

Use an ignored fresh build folder so tracked local artifacts in `tools/PCWord.TsfProbe/build/` are not changed:

```powershell
cmake -S .\tools\PCWord.TsfProbe -B .\tools\PCWord.TsfProbe\build-validation -A x64
cmake --build .\tools\PCWord.TsfProbe\build-validation --config Release
ctest --test-dir .\tools\PCWord.TsfProbe\build-validation -C Release --output-on-failure
```

These native policy tests require a Windows SDK and do not activate the COM text service. The current environment's native result must be reported separately from the passing managed suite.

### Manual Windows verification (not yet run)

Use only synthetic text and a disposable test profile. For each check, record OS/build and pass/fail without copying text into notes.

1. Start with `dotnet run --project .\src\PCWord.Stage0\PCWord.Stage0.csproj`; load the sample and confirm the version/focus status changes without displaying the document text in diagnostics.
2. Type a two-character-or-longer partial word at its end; verify suggestions appear only inside PCWord's window and the editor remains focused. Try caret movement into the middle of a word, punctuation, backspace, and caret movement to the end; stale candidates should disappear/update.
3. Select text and confirm suggestions and acceptance are suppressed. Clear selection and confirm fresh context can resume.
4. Toggle the enabled control off and on. While off, verify context collection and candidates stop; when re-enabled, only current context may produce candidates.
5. Mark the field sensitive and verify no suggestions or acceptance. Remove the flag and verify collection resumes only after a fresh context update.
6. Try Escape and each available Ctrl+Space/Ctrl+1–3 acceptance. Check exact resulting word and caret, including a prefix near the 128-unit left boundary; a possibly truncated word must be refused.
7. Exercise IME composition with synthetic text: no suggestions during composition; after composition ends, only a new snapshot may resume.
8. Close the editor while an edit request is pending if the timing is reproducible; confirm clean refusal/cancellation and no hang.
9. Separately build/register the TSF diagnostic probe per its README in a disposable account, reproduce `GetValue` using synthetic text, record method/HRESULT/scope category only, then unregister. No production context read is enabled by diagnostic opt-in alone.
10. Run UI Automation probes only as documented. Test each target app/field independently; mark observations unverified until actually executed. No cross-app support is inferred from probe metadata.
