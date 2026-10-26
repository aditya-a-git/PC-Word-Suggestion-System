# Stage 0 architecture decisions

## ADR-0001: prove text context in an owned editor before selecting a system adapter

**Status:** Provisional; controlled-editor proof implemented, Windows integration decision open.

**Decision.** The initial executable is a .NET 8 WinForms application containing its own multiline text field. A platform-independent `TextContext` value computes a caret-relative word, bounded preceding context and explicit replacement. This demonstrates the application-side contract without pretending a global hook can supply text context.

**Evidence.** The harness is implemented in `src/PCWord.Stage0`; the context model and automated cases are in `src/PCWord.Core` and `tests/PCWord.Tests`. CI has compiled and tested the solution using .NET 10 on Windows. The interactive WinForms proof still requires manual execution and a documented test run. It exposes field focus and selection state and exercises explicit replacement with suffix preservation.

**Trade-offs.** WinForms is minimal and available as a Windows desktop target. It tests only a field owned by the harness and provides no evidence about browser, Office, terminal, editor, remote or elevated process integration. Context offsets use UTF-16, consistent with .NET text controls. Combining marks and grapheme-cluster editing still need focused cases.

**Alternatives investigated on paper.** TSF is the most promising first system integration to investigate because it exposes text-store/context and edit-session concepts. A low-level keyboard hook observes keyboard events but cannot reliably recover text/caret state or perform verified edits by itself. UI Automation/accessibility can expose text patterns and selection in some providers but has provider and application variability. A hybrid should be selected only from test evidence.

**Consequences / next decision gate.** Run the controlled editor and complete the manual matrix for a Win32 control, browser, Office, VS Code and terminal. Separately prototype TSF text-context/edit operations and compare lifecycle/registration/deployment cost. Until that evidence exists, no production global input path is selected and unsupported/unknown fields must yield no context or edit.

## ADR-0002: platform-neutral input context contract and conservative prediction context

**Status:** Implemented as a platform-neutral contract; no production Windows adapter selected.

**Decision.** Input integrations publish versioned `InputContextSnapshot` values with a field identity, bounded text before/after the caret, selection length, focus, sensitivity, composition and context-availability state. They implement `IInputAdapter` and explicit versioned edit requests. The prediction-side `PredictionContextManager` rejects old/equal versions, caps retained context, and only enables prediction for focused, known-safe fields with no selection or active composition.

**Rationale.** A keyboard hook alone is insufficient to establish text context or safe edits. The prediction library should receive explicit context facts rather than infer them from raw keystrokes. Unknown sensitivity and unavailable context are not treated as safe.

**Limitations.** The contract does not prove that TSF, UI Automation, or another adapter can provide every field reliably. The manager stores only the latest bounded snapshot; it does not retain document history, persist text, or log it. A real adapter must verify field identity, context version and expected text before applying an edit. IME composition is conservatively excluded rather than interpreted.

**Exit gate.** Simulated unit tests validate context classification, bounds, field changes, invalidation, sensitivity/composition/selection exclusions and stale-version rejection. Stage 2's live adapter integration remains pending the Stage 0 Windows API feasibility decision and manual testing.
