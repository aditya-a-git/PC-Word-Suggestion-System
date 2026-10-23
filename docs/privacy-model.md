# Stage 0 privacy model

## Data handled

The controlled editor necessarily holds the user's current editor text in its normal WinForms text control. `TextContext` receives a transient in-process snapshot when the UI refreshes. It computes the current word and a bounded prefix (default maximum 128 UTF-16 units); it does not store history, write content to disk, emit text in logs, or send it over a network. The explicit edit occurs only after the user clicks the edit button.

## Sensitive and unavailable fields

The harness checkbox simulates a sensitive field: context becomes ineligible and explicit edits are refused. Focus loss and selection also make the prediction-oriented context ineligible. A real application integration must identify sensitive fields reliably or conservatively suppress context where sensitivity is unknown; this harness does not detect password fields in other applications.

## Boundaries

There is no global keyboard hook, key event recorder, telemetry, network request, personalization store, dictionary, or prediction component in Stage 0. No raw keystrokes are collected. The future adapter must avoid elevated privileges and must not bypass process security boundaries. Logging, if added later, must be metadata-only by default and must never contain typed text.

## User controls and retention

The harness process holds its text in memory as a normal editor would; closing the process ends that in-memory session. No persistence or personalization exists. The sensitive checkbox is a test control, not a substitute for a future global pause/status control.
