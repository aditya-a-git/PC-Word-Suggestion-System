# Stage 0 and Stage 2 privacy model

## Data handled

The controlled editor necessarily holds the user's current editor text in its normal WinForms text control. `TextContext` receives a transient in-process snapshot when the UI refreshes. It computes the current word and a bounded prefix (default maximum 128 UTF-16 units); it does not store history, write content to disk, emit text in logs, or send it over a network. The explicit edit occurs only after the user clicks the edit button.

Stage 2 introduces a platform-neutral `InputContextSnapshot` contract and `PredictionContextManager`. The adapter supplies only a bounded view around the caret. The manager retains only the latest bounded context in memory (up to the configured preceding/following limits), with a monotonically increasing version. It does not persist context, log text, collect a raw keystroke stream, or retain a document history. When the manager is invalidated, its current context is cleared.

## Sensitive and unavailable fields

The controlled harness checkbox simulates a sensitive field: context becomes ineligible and explicit edits are refused. This is not a substitute for a future global field-sensitivity detector.

Stage 2 only permits prediction when the adapter reports the field as `KnownSafe`, focus is present, the field has an identity, no text selection exists, context is available, and IME composition is not active. `Unknown` sensitivity, sensitive fields, missing context, missing focus, selection and composition are conservatively excluded. The contract cannot independently verify whether an adapter's sensitivity report is correct.

## Windows UI Automation feasibility probe

The read-only probe at `tools/PCWord.WindowsProbe` polls the focused UI Automation element. By default, it reports metadata without reading surrounding text. The explicit `--allow-context-read` flag enables transient reads of at most 128 preceding and 32 following characters only to determine whether the provider exposes bounded context. It prints metadata and character counts, never text content, and does not persist snapshots or log key events. It skips context reads when UI Automation marks the field as a password field or when a selection is present.

This remains a feasibility tool, not a production adapter. UI Automation does not universally expose sensitivity, and a provider may expose text even when a user considers it private. Use only synthetic text during testing. The probe does not attempt replacement.

## Boundaries and adapter obligations

There is no global keyboard hook, key event recorder, telemetry, network request, personalization store, or production system-wide input adapter implemented. The UI Automation probe does not edit text. The contract does not claim compatibility with other applications. A future adapter must not elevate privileges unnecessarily or bypass process security boundaries. Before applying an edit, it must check the active field identity, context version and expected text, and must return an explicit failure status when the edit is stale, unsupported, refused, or fails.

Logging, if added later, must be metadata-only by default and must never contain typed text.

## User controls and retention

The harness process holds its text in memory as a normal editor would; closing the process ends that in-memory session. The Stage 2 manager keeps only its current bounded context. No persistence or personalization exists. A global pause/status control is still a future application-shell requirement.
