# Privacy model

## Normal controlled-editor path

PCWord's WinForms `RichTextBox` retains its document as ordinary editor state. The input adapter requests at most 128 UTF-16 units before and 32 after the caret, plus focus, selection, composition state, sensitivity toggle, context version, caret offset and caret bounds. It does not copy the whole document into prediction state. The current bounded context and up to three candidates are transient in-process values.

The adapter reads no text when suggestions are paused, the editor is unfocused, a selection exists, IME composition is active, the sensitive toggle is enabled, or the control/context is unavailable. The adapter has authority only over PCWord's own editor. The sensitivity checkbox is a local explicit control, not an automatic classifier for arbitrary applications.

Acceptance requires a deliberate shortcut. The adapter checks version, field identity, current candidate provenance, current text and eligibility, replaces only the current prefix, and verifies resulting text and caret. Unsupported, stale, sensitive, or unavailable cases do not edit.

## Aggregate performance measurements

The application stores only numeric durations, stage enums, and sample counts. Each stage retains a fixed ring of at most 2,048 timing values plus a lifetime count. The measured stages are dictionary startup, context acquisition, lookup/ranking, result delivery, UI update, end-to-end prediction, and lookup-only. Metrics contain no text, prefix, candidate, field identity, or per-input event record. They stay in process memory, are not written to disk or sent over a network, and disappear when the process exits. The app shows them only when its Performance stats control is used.

The benchmark uses fixed synthetic prefixes and prints aggregate timing/resource values only. Its synthetic end-to-end number excludes RichTextBox/Win32 reads and actual painting. See `docs/benchmarks.md` for measured results and limitations.

## Offline behavior and source inspection

The normal path reads the bundled dictionary file locally. A scoped source search over `PCWord.Core`, `PCWord.Prediction`, and `PCWord.Stage0` found no network-client, content-logging, or content-write API matches. This is not a runtime packet capture or a repository-wide security audit. No prediction request needs network access.

## Feasibility tools

The UI Automation probe is read-only and provider-dependent. The TSF diagnostic bounded-read path requires `PCWORD_TSF_ALLOW_CONTEXT_READ=1` in the host process, plus valid per-user settings at `HKCU\Software\PCWord`, `Enabled=1`, an exact host executable filename on the allowlist, and an eligible TSF context. The managed app creates defaults only when the settings key is absent: `Enabled=0` (paused) and `Allowlist=[notepad.exe]`. Missing individual values, malformed values, or registry access failures deny reads. The native probe rereads the registry values on every TSF edit notification; a toggle takes effect at the next such notification. The diagnostic opt-in is checked as the final independent gate.

Sensitive classification is never bypassed by host allowlisting or diagnostic opt-in. The existing classifier marks InputScope values Password (31), Private (61), NumericPassword (63), NumericPIN (64), AlphanumericPIN (65), and AlphanumericPINSet (66) as Sensitive. The lookup path is `GetAppProperty(GUID_PROP_INPUTSCOPE)` → `GetValue` → `QueryInterface(ITfInputScope)` → `GetInputScopes`; a failed or unknown result remains Unknown. Unknown is allowed only for an explicitly allowlisted host. InputScope and UIA metadata remain hints, not a general proof that content is non-sensitive. Do not allowlist browsers by default: an Unknown non-password field containing card numbers or OTPs would pass the app-level gate.

The per-user registry stores only the Enabled flag and executable names. The hotkey is registered by the managed PCWord app as Ctrl+Alt+Shift+P with MOD_NOREPEAT; if registration fails, the UI reports that and its checkbox remains usable. No raw keys are captured by a hook or TSF key sink. Decision output contains only a reason code and allowed/denied result. No typed text, window title, document name, field contents, or raw keystrokes are logged. The allowlist matches only the host executable filename, case-insensitively and exactly; a differently located binary with the same name can match. Full path or signature validation is deferred.

These gates control diagnostic context reads only. No external-app prediction path is implemented or supported. The probes are not an automatic production fallback.

No global keyboard hook, raw key event recorder, text telemetry, personalization store, cross-process IPC, or TSF edit session exists. No typed text is logged or persisted by the normal prediction path.
