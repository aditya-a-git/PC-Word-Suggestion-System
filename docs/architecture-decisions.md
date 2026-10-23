# Stage 0 architecture decisions

## ADR-0001: prove text context in an owned editor before selecting a system adapter

**Status:** Provisional; controlled-editor proof implemented, Windows integration decision open.

**Decision.** The initial executable is a .NET 8 WinForms application containing its own multiline text field. A platform-independent `TextContext` value computes a caret-relative word, bounded preceding context and explicit replacement. This demonstrates the application-side contract without pretending a global hook can supply text context.

**Evidence.** The harness is implemented in `src/PCWord.Stage0`; the context model and automated cases are in `src/PCWord.Core` and `tests/PCWord.Tests`. It exposes field focus and selection state and exercises explicit replacement with suffix preservation. Automated tests are authored but were not run in the current environment because only .NET runtimes are installed; no SDK, compiler or MSBuild was available. The interactive WinForms proof therefore also remains unverified until run on Windows with an SDK.

**Trade-offs.** WinForms is minimal and available as a Windows desktop target. It tests only a field owned by the harness and provides no evidence about browser, Office, terminal, editor, remote or elevated process integration. Context offsets use UTF-16, consistent with .NET text controls. Combining marks are currently treated as delimiters unless classified as letters/digits by Unicode Rune APIs; grapheme-cluster editing deserves a later focused case.

**Alternatives investigated on paper.** TSF is the most promising first system integration to investigate because it exposes text-store/context and edit-session concepts. A low-level keyboard hook observes keyboard events but cannot reliably recover text/caret state or perform verified edits by itself. UI Automation/accessibility can expose text patterns and selection in some providers but has provider and application variability. A hybrid should be selected only from test evidence.

**Consequences / next decision gate.** Run the controlled editor and complete the manual matrix for a Win32 control, browser, Office, VS Code and terminal. Separately prototype TSF text-context/edit operations and compare lifecycle/registration/deployment cost. Until that evidence exists, no production global input path is selected and unsupported/unknown fields must yield no context or edit.
