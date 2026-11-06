# Production Windows integration architecture

## Status and evidence

This document describes the intended boundaries and the managed protocol foundation currently implemented. It is **not** a claim that a production external-app adapter is functional.

The previous TSF feasibility gate remains open: there is no dated/versioned interactive host run, and the TSF diagnostic probe has no write edit session. Therefore production activation, context acquisition, and external edits must not be enabled or advertised yet.

Implemented in this stage:
- `src/PCWord.WindowsIntegration` defines a bounded, versioned message contract for context updates, suggestions, acceptance, edit results, and pause events.
- The codec validates request IDs, protocol version, snapshot identity/version, field-ID length, candidate count/length, enum values, and an 8 KiB frame ceiling.
- `StaleRequestGate` cancels a prior prediction when newer context begins and requires request ID, context version and field identity to match before a result is current.
- Managed tests cover malformed/oversized messages, identity mismatches, candidate bounds, cancellation and disposal.

Not implemented:
- Named-pipe server or native client.
- Process authentication and pipe ACL configuration.
- TSF context acquisition in the production project.
- An `ITfEditSession` acceptance path.
- Suggestion window for external apps, service startup/recovery, or live end-to-end integration.

## Intended boundaries

1. **Native TSF service (in-process in each host application):** observes TSF lifecycle/focus, checks per-user policy, obtains a short-lived read-only bounded snapshot under an authorized read session, and sends metadata plus context over a local authenticated IPC channel. A write operation would be requested only after independent runtime validation.
2. **Managed prediction service (PCWord user process):** owns dictionary/model state and invokes `PredictionContextManager` and existing completion logic. It receives bounded context messages and produces suggestion responses tied to the same request/context identity.
3. **Communication:** local named pipe with explicit protocol version, request correlation ID, monotonic context version, bounded UTF-8 frames, strict parsing, timeouts, cancellation and reconnect. The client must authenticate the server process and the server must authorize the client/host policy. Current-user-only ACL alone does not authenticate a trusted process against another process running as the same user, so it is not sufficient by itself.
4. **Suggestion presentation:** separate managed UI component. It must display a result only after checking the current request ID, context version, field identity and enabled/allowed state.
5. **Acceptance:** UI returns the original request/context identity and chosen candidate. Native TSF must revalidate focused document/context, current generation, selection, expected text, sensitivity and allowlist inside the authorized write session. A stale or refused request clears the suggestion and reports failure without fallback keystrokes.

## Message contract

`AdapterMessage` JSON fields are:
- `protocolVersion` (currently 1)
- `kind` (`ContextUpdate`, `Suggestions`, `AcceptSuggestion`, `EditResult`, or `Pause`)
- `requestId` (non-empty GUID)
- `contextVersion` (non-negative and equal to snapshot version where present)
- `fieldId` (bounded to the core contract's maximum)
- optional typed payload fields (`snapshot`, up to three `suggestions`, `candidate`, or `editStatus`)

The codec serializes only validated messages, rejects unknown JSON properties and caps each UTF-8 frame at 8 KiB. Context values are sensitive transient data even though each is bounded; transport callers must not log or persist frames. The managed contract is tested, but it is not yet carried over a pipe.

## Lifecycle and recovery

The intended flow is asynchronous and fail-closed: the TSF callback never waits for a prediction response; context and acceptance requests are queued to a worker. A failed/disconnected service clears candidates, invalidates the request gate, and denies edits. Pause, disallow, sensitive scope, focus change, context change, service disconnect and shutdown all invalidate outstanding request IDs. Cancellation is advisory; the gate's identity/version check is still required at result delivery and acceptance.

No background work may retain a live document pointer for later editing. A future native write request must hold an AddRef'd context only for request lifetime, execute the stale check inside `ITfEditSession::DoEditSession`, use a write lock granted by `ITfContext::RequestEditSession`, and release all references on completion or destruction. It must fail if the target context/document is no longer current.

## Security model required before enabling IPC

- Per-user pipe namespace and restrictive DACL.
- Authenticate the PCWord service process from the native TSF client; authenticate/authorize the caller on the service side. Do not treat pipe-name secrecy or current-user ACL as process authentication.
- Enforce app allowlist and settings again in the native process immediately before every read/edit. The managed side also validates the supplied metadata but does not trust it as proof of host identity.
- Bound frames before allocation, use strict UTF-8/JSON parsing, reject unknown fields/versions, and rate-limit or disconnect malformed clients.
- Keep text only for the active request; do not include it in logs, exception text, crash reports or persistent state.
- Start paused/disabled by default. Disconnect, policy-read failure and uncertain sensitivity all deny.

## Verification gates

1. Run the Windows TSF read-only host matrix in `docs/testing-guide.md` and record real versions/results.
2. Implement the named-pipe transport and verify peer authentication, malformed clients, timeout/restart and rapid-context invalidation.
3. Implement a separate write-session experiment. Verify stale/focus change rejection and post-edit result with synthetic text in a controlled test host before integrating acceptance.
4. Exercise end-to-end flow in Notepad and record manual verification; expand to other apps only with per-app evidence.
5. Measure context, IPC, prediction, presentation and edit latency separately (median/p95) under a recorded Windows build. Until measured, no latency claim is made.
