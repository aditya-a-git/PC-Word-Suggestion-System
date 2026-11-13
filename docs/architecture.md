# Architecture and implementation status

The repository contains an implemented controlled-editor typing assistant and a
separate experimental Windows integration path. It is not production-ready.
The [audit](implementation-audit.md) records the inspected baseline and defects.

| Layer | Responsibility and implemented boundary |
|---|---|
| PCWord.Core | Bounded UTF-16 snapshot, field/version identity, edit validation, settings and candidate contracts. `Validate()` also checks copies created with record `with`. |
| PCWord.Prediction | Existing ESDB dictionary, prefix ranking, n-gram inference, context manager and coordinator. No native API, networking or alternate prediction engine was added. |
| PCWord.WindowsIntegration | Version 1 managed message validation and single-current-request gate. No transport or authenticated service yet. |
| PCWord.WindowsUI.Logic | Pure placement and candidate-state logic. |
| PCWord.WindowsUI | Separate WinForms no-activate popup, UI-thread dispatch and coordinator acceptance bridge. No external input route yet. |
| src/PCWord.Stage0 | Runnable owned RichTextBox app, adapter and settings. |
| Stage0/ | Earlier controlled-editor feasibility tool, outside the solution. |
| PCWordTsfProbe.dll | Original diagnostic TIP, bounded read/status path. No writes or key interception in this build. |
| PCWordTsfEditExperiment.dll | Separate CLSID/profile, explicit synthetic read/write experiment, synchronous TSF sessions on preserved keys only. No prediction, IPC or popup. |

The experimental TIP stays in the host's TSF apartment. It verifies the focused
thread, process, document's top context, selection, composition, integrity level
and policy before reading. A capture holds one range, a generation, a five-second
ticket and a SHA-256 fingerprint of the bounded window; raw context is not stored
between capture and apply. A ticket is consumed before attempting the edit.
The range implementation and session lifetime are separated for native tests.

The coordinator remains synchronous for prediction. Acceptance is serialized and
consumes its candidate set, including when the adapter throws, cancels or returns
an uncertain failure. A completion for an old context cannot clear newer results.
Adapters still own actual edit authorization and post-edit verification. Coordinator
calls/context events remain confined to their owning thread; the acceptance guard
does not make the entire coordinator a concurrent service.

The stale-request gate swaps state under a lock and cancels outside it, so callback
reentrancy cannot overwrite a newer ticket or deadlock the lock. Versions strictly
increase for each gate lifetime. Cancellation exceptions increment a numeric
`CancellationFailureCount`; no callback message or user text is recorded.

Migration: protocol version stays 1, with stricter validation of duplicate/missing
properties, Unicode, enum values and private/ineligible payloads. Callers supplying
malformed messages now fail with `InvalidDataException`. A reconnect must use a
new gate only after authenticating a new adapter lifetime. No deployed transport
exists to migrate. Existing dictionary/model formats and normal app settings are
unchanged. See [TSF](tsf-integration.md) and [IPC](ipc-security.md).
