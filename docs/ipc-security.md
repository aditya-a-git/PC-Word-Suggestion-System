# IPC security and protocol status

No named-pipe transport or peer authentication is implemented. The JSON codec and
request gate are foundations only; they are not a secure communication channel.
Priority 2 is blocked on the real-host TSF gate. No listener is opened by this change.

Version 1 messages contain `ProtocolVersion`, `Kind`, a nonempty GUID `RequestId`,
nonnegative `ContextVersion`, and bounded `FieldId` plus a kind-specific payload:

| Kind | Payload |
|---|---|
| ContextUpdate | Snapshot with matching identity/version; no candidate/result fields |
| Suggestions | At most three unique, well-formed, non-control candidates, ≤256 UTF-16 units each |
| AcceptSuggestion | Nonempty field and one candidate |
| EditResult | Field identity and defined TextEditStatus |
| Pause | No payload |

Encoded payloads must be 1–8192 bytes. Decoder rejects unknown/duplicate properties,
missing required constructor parameters, unsupported versions, undefined/numeric
enums, malformed Unicode, invalid geometry and excessive nesting. Validation also
checks in-memory snapshot copies; constructor-only validation was insufficient.
Ineligible (sensitive/unknown, unfocused, selected, composing or unavailable)
contexts may carry invalidation metadata but no text. Errors expose fixed messages,
not payloads or nested exceptions. The codec does not provide framing or preallocation
checks for a stream because there is no stream transport yet.

The gate binds results to request ID, strictly increasing version and field. It
invalidates on cancellation/disposal and rejects duplicate/decreasing versions.
It does not itself authenticate a connection, enforce timeouts, or authorize an
edit. A producer must still perform the final current-ticket check when dispatching.

## Threat model and remaining guarantees

Potential peers include another user's process, another logon session, an
unauthorized process under the same user, an impersonating server, a replaced
process/PID, and elevated or lower-integrity processes. Current-user ACLs and
executable filenames alone do not distinguish trusted same-user software. The
in-process TIP can be loaded into arbitrary target processes; its host executable
is not inherently the identity of the PCWord code.

Before implementing transport, settle a deployment identity that Windows can
validate (for example a signed packaged broker plus a scoped, authenticated session
bootstrap). Validate both peers, user/logon session and integrity, bind process
handles/creation identity against PID reuse, reject remote pipe clients, set
restrictive explicit ACLs, and reject unexpected service instances. Signing alone
does not isolate arbitrary processes with the same user/token or a compromised host.
Do not promise protection from a malicious same-user host without a defensible
bootstrap and trust boundary.

The future transport must length-check before allocation, bound connections/queues,
use finite connection/request deadlines, correlate replies, cancel obsolete work,
handle disconnect/restart, and invalidate UI/edit capabilities on every failure.
No unsafe fallback to SendInput or clipboard injection is permitted. None of these
transport or authentication acceptance criteria has been verified.
