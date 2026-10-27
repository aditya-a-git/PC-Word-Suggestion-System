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
