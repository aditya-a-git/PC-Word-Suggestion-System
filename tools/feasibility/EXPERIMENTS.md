# Stage 0 Windows API experiments

## Implemented harness

The controlled editor is `src/PCWord.Stage0`. Use only synthetic disposable text. Load the sample, inspect focus/caret/current-word/context values while moving the caret, select text, move focus, toggle the sensitive-field flag, and replace a partial word. Compare the final entire string and caret with the intended edit. Repeat at word start/middle/end and adjacent to punctuation. Record OS build and result in the compatibility matrix. This validates only the owned WinForms field.

## Text Services Framework (proposed; unverified)

Create a separate minimal native/COM experiment on a Windows 11 development machine. First enumerate/document TSF activation and text-service registration requirements; do not register a production service or change machine-wide settings for this harness. In an isolated disposable build, implement the minimum text-service interfaces needed to acquire the focused document manager/context, request a read edit session, retrieve selection and bounded surrounding text, then request a write edit session to replace a selected/current-word range. Log only pass/fail, HRESULTs, offsets, app/version and timings—never text or key events. Verify exact resulting text, selection/caret and focus in a controlled editor, then unregister/remove the experimental registration. Test 32/64-bit and per-user deployment needs before deciding production architecture. Mark application/app-version results separately; a single host is not broad compatibility.

## Low-level keyboard hook (proposed; unverified)

Use a disposable process and an isolated test account. If investigating `WH_KEYBOARD_LL`, count event types and timestamps only; do not record key codes, characters, typed strings or replay input. Measure callback latency and behavior on focus changes and process shutdown; ensure the callback promptly chains to the next hook and always unhooks on clean exit. Separately test whether any legitimate text/caret API supplies context; the hook itself is not evidence of context access or safe edit capability. Do not deploy globally or run elevated. Document security/software compatibility concerns and remove the hook process after testing.

## Accessibility / UI Automation (proposed; unverified)

For each named target app and exact version, inspect UI Automation TextPattern availability, text range/selection and bounding rectangles using synthetic content. Do not read unrelated controls or persist strings. Attempt a replacement only through a supported provider operation in a disposable document; verify exact final text and caret. Record provider absence, stale ranges and failure behavior. Password/sensitive fields must be excluded. UIA outcomes are app/provider-specific.

## Overlay behavior (proposed; unverified)

No popup/overlay is implemented in Stage 0. In a later isolated prototype, use a non-activating top-level window (for example, evaluate Win32 `WS_EX_NOACTIVATE`/`ShowWindow` behavior) and synthetic coordinates from the controlled field. Verify active-window/focus identity before and after display, typing, Escape and dismissal, multi-monitor/DPI positioning, and click behavior. Do not present predictions or perform edits in this experiment.

## Representative application procedure

On Windows 11, record OS build and application version. Test separately in a basic Win32 text editor, browser input and contenteditable, Office editor, VS Code, and terminal. Use only disposable synthetic content. For each, record context access, caret/selection, focus-change handling, explicit edit correctness, and failure/sensitive handling. Unknown API results remain “unknown”; do not infer compatibility from the controlled editor. None of these external experiments has run as part of this implementation.
