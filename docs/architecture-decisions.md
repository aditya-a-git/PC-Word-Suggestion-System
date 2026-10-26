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


## ADR-0003: controlled-editor non-activating popup prototype

**Status:** Implemented as a Stage 3 controlled-editor experiment; not selected as the production system-wide popup or input architecture.

**Decision.** Add a small WinForms popup with non-activation window styles, up to three candidates, keyboard acceptance (Ctrl+Space and Ctrl+1/2/3), and Escape dismissal. Keep the candidate state platform-neutral and versioned so out-of-order suggestion updates are rejected and acceptance requires an exact context version, field identity, and valid candidate index. The controlled editor also rechecks the text and selection snapshot immediately before its local replacement.

**Evidence and scope.** The popup is hosted by the Stage 0 WinForms application and positions itself using the owned RichTextBox caret geometry. A small fixed demonstration vocabulary is filtered by the typed prefix. Unit tests cover candidate count, case-insensitive deduplication, empty results, field clearing, stale versions, acceptance validation, and dismissal. CI compilation/tests and a manual Windows interaction run are separate forms of evidence; the latter remains required.

**Trade-offs and limitations.** WinForms non-activation flags are a prototype, not proof of focus behavior across Windows configurations. Owned-control caret coordinates do not demonstrate cross-process caret geometry. Mouse selection is omitted because a click could activate the popup and disrupt typing. The demo vocabulary is temporary; Stage 4 connects the actual dictionary engine. The controlled editor does not implement the Stage 2 IInputAdapter or provide system-wide typing support.

**Exit gate.** On Windows, manually verify popup positioning, no focus theft during typing, keyboard acceptance, Escape dismissal, sensitive-field suppression, stale-context rejection, and exact text/caret after edits. Do not claim Stage 3 complete until those interactive checks pass.


## ADR-0004: Keep the Stage 3 suggestion surface inside the controlled editor

**Status:** Implemented as a controlled-editor prototype; not a system-wide overlay decision.

**Context.** The first popup used a separate top-level WinForms Form. Manual testing reported that the editor lost focus and typing stopped when the popup appeared.

**Decision.** Replace the top-level Form with a compact child Panel hosted inside the controlled editor window. Child labels are non-selectable, and suggestions are suppressed until the prefix has at least two characters. This prevents a second top-level window from being shown and reduces noise from single-letter prefixes.

**Trade-offs and limits.** The child panel can only appear inside PCWord's own window. It does not solve caret tracking or explicit edits in other applications. Manual testing is still required to confirm keyboard focus retention, persistence while typing, dismissal, and edit correctness.
