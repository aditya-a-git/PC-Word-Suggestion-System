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


## ADR-0005: use UI Automation as a read-only feasibility probe, not as the final input adapter

**Status:** Probe implemented; application compatibility and edit path unverified.

**Context.** The system-wide goal requires evidence that Windows can expose focused-field identity, text around the caret, selection state, and caret geometry without interrupting typing. A keyboard hook alone cannot provide this complete information.

**Decision.** Add `tools/PCWord.WindowsProbe`, a small Windows/.NET console probe using UI Automation `TextPattern`. It reports focused process/control metadata, provider capability flags, and bounded character counts. It is deliberately read-only and does not send input or edit text. It skips surrounding text when the provider marks the focused element as a password field or when a selection is present. It never prints or persists text content.

**Trade-offs and limitations.** UI Automation providers differ by application and field type. Password metadata is not a universal sensitivity signal; a non-password field may still contain private information. The probe demonstrates only what a particular provider exposes at a particular moment. It does not establish stale-context-safe replacement, a non-activating global overlay, or system-wide compatibility.

**Next decision gate.** Run the documented synthetic-text matrix on Notepad, browser text input and contenteditable, VS Code, an office editor, and terminal. Compare the observations with a native TSF text-service proof of concept. Do not connect the prediction engine to other applications or claim system-wide support until both context acquisition and an explicit edit can be verified without duplicated/lost characters or focus theft.


## ADR-0006: Keep TSF integration in feasibility mode until field safety and edit semantics are proven

**Status:** Superseded by ADR-0008 for diagnostic context-read gating; production integration remains unselected.

**Evidence from API research.** Microsoft documents ITfContext::GetAppProperty(GUID_PROP_INPUTSCOPE, ...) and ITfReadOnlyProperty::GetValue(...) as the input-scope lookup path. The API can report that the context owner does not support a property, and an unspecified E_FAIL does not establish whether a field is safe. Microsoft also requires text modifications to occur through an authorized TSF edit session; the current probe's read-only notification callback must not be used for insertion.

**Decision.** Keep the native TSF component as a read-only feasibility probe until a Windows runtime test proves stable, bounded context access in a controlled editor and verifies an explicit edit in a separate authorized edit-session experiment. Classify Password, PIN, and Private input scopes as sensitive. Unknown scope, unsupported property, or any lookup failure denies context reads. The UI Automation probe remains diagnostic-only and is not a fallback that automatically reads or edits arbitrary fields.

**Consequences.** The current implementation does not yet provide a production cross-process adapter, system-wide popup, or system-wide acceptance operation. These must be implemented only after the supported field set and edit semantics are demonstrated. No broad compatibility claim is allowed based on the API design alone.

**Exit gate.** Build and run CTest on Windows, verify scope classification and opt-in behavior with synthetic text, record results by application/field, then implement version-checked edit acceptance through TSF edit sessions. Do not begin contextual next-word prediction until the integration gate is met.

## ADR-0007: use a bounded in-process adapter for the owned editor; keep production external integration disabled

**Status:** Implemented for PCWord's WinForms editor; production external Windows integration remains unsupported. Diagnostic TSF reads are separately gated by ADR-0008.

**Decision.** Implement `IInputAdapter` directly over PCWord's own `RichTextBox`. Read only bounded RichEdit ranges around the caret; publish a per-control identity and monotonically increasing snapshot version; suppress context for disabled, unfocused, selected, sensitive, composing, unavailable, or malformed contexts. Connect snapshots to the existing context manager and dictionary ranking pipeline. Apply only a current explicitly accepted prefix replacement and verify the resulting text/caret.

**Rationale.** The existing owned field supplies a stable text/control boundary and can make edits through its own selection APIs. The TSF probe has not established stable safe context access across hosts, an edit-session operation, or reliable sensitivity classification. UI Automation TextPattern is read-only and provider-dependent. A new IPC layer would not solve these unproven facts, so no cross-process transport or privileged access is added.

**Trade-offs and limits.** This is a real adapter implementation for the owned control, not an external-app adapter. It uses bounded reads, transient memory and no key hooks. Caret bounds come from the owned control and still require interactive verification. The native TSF `GetValue` E_FAIL remains unresolved and is treated as unknown. External applications receive no predictions or edits.

**Verification gate.** The managed build and automated tests pass, but the controlled WinForms UI must still be manually exercised with synthetic text. Do not claim cross-application compatibility or pass the Stage 4 integration gate until a supported host demonstrates safe context, authorized edit behavior, and popup focus/positioning in repeatable tests.


## ADR-0008: gate diagnostic TSF context by explicit per-user host approval and pause state

**Status:** Implemented as a diagnostic read policy; external prediction remains unsupported.

**Context and evidence.** The repository's TSF code obtains InputScope through `GetAppProperty(GUID_PROP_INPUTSCOPE)`, `GetValue`, `QueryInterface(ITfInputScope)`, and `GetInputScopes`. The observed `GetValue` result is E_FAIL in some tested fields, leaving classification Unknown. Existing `InputScopePolicy.h` marks Password (31), Private (61), NumericPassword (63), NumericPIN (64), AlphanumericPIN (65), and AlphanumericPINSet (66) as Sensitive. A project owner reports browser password detection but supplied no repository log, so this remains reported rather than verified evidence. Microsoft documents InputScope as guidance, not proof that a field cannot contain sensitive data.

**Decision.** Use a single pure, ordered native policy decision. Missing/unreadable/malformed settings deny; otherwise paused denies; Sensitive always denies; unavailable/unfocused/non-collapsed/composing context denies; a host outside the exact user allowlist denies; then an allowlisted host with Unknown scope receives the distinct `AllowedByHostPolicy` result and DeclaredNonSensitive retains a separate `AllowedByDeclaredScope` result. The `PCWORD_TSF_ALLOW_CONTEXT_READ=1` diagnostic opt-in is an additional final gate. The app owns pause state through `HKCU\Software\PCWord\Enabled` and registers Ctrl+Alt+Shift+P with MOD_NOREPEAT. The native TSF service rereads `Enabled` and `Allowlist` on each edit notification. When the whole settings key is absent, the managed app creates `Enabled=0` and a `REG_MULTI_SZ` allowlist containing only `notepad.exe`; if an existing key is partial or malformed, it is not repaired automatically and the native side denies.

**Host matching and privacy.** The TSF service takes the current process executable name from `GetModuleFileNameW(nullptr, ...)`, extracts only the file name, and compares case-insensitively and exactly. Empty names, whitespace, path separators, and wildcard characters are invalid. A different binary at another path can use an allowlisted name; path/signature validation is deferred. Do not add browsers by default: when scope is Unknown, card-number and OTP fields that are not marked as passwords may be admitted by the app-level rule. The decision log contains only its reason code and allowed/denied result; it never contains text or host/window/document identity.

**Alternatives rejected.** UI Automation `IsPassword` alone is deferred because it does not identify other sensitive non-password content and offers no authorized write path. TSF-only scope gating is insufficient for hosts returning E_FAIL, while treating all Unknown fields as allowed would be unsafe; the explicit host allowlist is the narrow override. No IPC, UIA fallback, key sink, keyboard hook, edit session, or external prediction was added.

**Consequences and limits.** Default state is paused and the allowlist contains only Notepad. File-name allowlisting is user approval of an application identity by name, not a guarantee about an individual field; Unknown is allowed only after that explicit app-level choice and with the separate diagnostic opt-in. The user toggle remains operable if hotkey registration fails, and the UI reports that failure. Settings take effect on the next TSF edit notification. This implements only bounded diagnostic context reads; it does not enable production prediction or editing in external apps. Owner-reported elevated registration is documented separately and has not been independently reproduced.
