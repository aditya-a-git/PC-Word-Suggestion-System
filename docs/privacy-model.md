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

The UI Automation probe is read-only and provider-dependent; explicit context-read mode is for synthetic data only. The TSF diagnostic probe requires its separate opt-in and fail-closed scope policy. InputScope/UIA metadata are hints, not proof that content is non-sensitive. Missing, malformed, failed, ambiguous, or unknown classifications remain denied. Neither probe is an automatic production fallback.

No global keyboard hook, raw key event recorder, text telemetry, personalization store, cross-process IPC, or TSF edit session exists. No typed text is logged or persisted by the normal prediction path.
