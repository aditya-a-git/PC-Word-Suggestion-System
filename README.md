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

## Windows integration feasibility probe

A read-only UI Automation probe is available at `tools/PCWord.WindowsProbe`. Run it on Windows:

    dotnet run --project .\tools\PCWord.WindowsProbe\PCWord.WindowsProbe.csproj

By default, the probe reports focused-control metadata without reading surrounding text. Only when focused on synthetic test text, explicitly opt in to bounded context reads with `dotnet run --project .\\tools\\PCWord.WindowsProbe\\PCWord.WindowsProbe.csproj -- --allow-context-read`. It never prints text content and does not edit text; it is not a system-wide typing adapter. Follow `tools/PCWord.WindowsProbe/README.md` and record observations in `docs/compatibility-matrix.md`.

## Windows TSF feasibility probe

A native C++ Text Services Framework (TSF) probe is available in `tools/PCWord.TsfProbe`. It is an experimental, read-only in-process text service. It observes TSF activation, document focus and edit notifications; it reports only status metadata, bounded character counts and caret-rectangle availability through the debugger output stream. It never prints text content or modifies text. Context reads require explicit opt-in and a positive safe scope; Password, PIN, and Private scopes are blocked.

See `tools/PCWord.TsfProbe/README.md` for build, per-user registration, explicit synthetic-text opt-in, test steps and uninstall instructions. Do not register this prototype on a daily-use machine unless you are comfortable testing a native COM DLL. It is not a production input adapter or a system-wide suggestion UI. See `docs/windows-integration-status.md` for the researched API path, implementation boundary, and remaining verification gates. Windows CTest targets cover the fail-closed opt-in and sensitive-scope policies.

## Scope and limitations

- The popup is a child control inside PCWord's own editor window. This avoids activating a separate top-level popup window, but is not a system-wide overlay.
- Caret geometry comes from the RichTextBox owned by this app. No cross-process caret acquisition or editing is implemented.
- Suggestions use a tiny hard-coded demo vocabulary. Dictionary integration remains separate work.
- Acceptance checks the current editor text, selection and caret snapshot before a local replacement. This does not implement the cross-process `IInputAdapter`.
- No typed text is logged or persisted. No production TSF adapter, global keyboard hook, or universal compatibility is claimed.

See `docs/testing-guide.md`, `docs/compatibility-matrix.md`, `docs/privacy-model.md`, and `docs/architecture-decisions.md`.

## Current integration boundary

The controlled editor now connects a bounded, in-process adapter to the existing local dictionary completion pipeline. The editor's own RichTextBox is the only supported source. The adapter suppresses unknown/unavailable, sensitive, selected, composing, unfocused, or disabled contexts and applies only a version-matched explicit prefix replacement. Automated managed tests pass; interactive Windows verification remains pending.

The TSF and UI Automation components remain feasibility probes. There is no cross-process adapter, IPC, TSF write edit session, system-wide popup, or external-application compatibility claim. No raw keystrokes are collected or logged. See `docs/windows-integration-status.md` and `docs/testing-guide.md` for exact scope and manual verification steps.
