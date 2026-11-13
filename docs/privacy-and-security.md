# Privacy and security status

The controlled editor uses only its own bounded context and local prediction.
No production external-app prediction, IPC or typed-content persistence was added.
The original [privacy model](privacy-model.md) remains relevant to that path.

The native scope classifier now makes a sensitive value dominate the entire
InputScope array, including Default/unknown followed by Password or PIN. Password,
private and PIN scopes deny reads/edits. Regression tests cover these orderings.
The TIP additionally checks actual foreground process/thread and focused top
context and rejects low/high integrity. Thread focus loss invalidates capture.

InputScope is advisory. It cannot reliably identify arbitrary credentials,
password-manager surfaces, browser payment/OTP inputs or private text typed into
an ordinary editor. Unknown scope remains permitted only by explicit diagnostic
host policy; the edit experiment additionally restricts to synthetic Notepad.
This diagnostic exception is **not a production privacy guarantee**. Browser,
Office, secure desktop, elevated, remote and custom-control support remains blocked.
No password text should be used in testing; verify with synthetic sensitive controls.

The experiment retains only a bounded hash and range/ticket between capture/apply.
Context strings exist during the edit callback; fixed buffers and owned strings are
cleared on normal destruction where practical. This is not a guarantee that the
host, OS or allocator has no copies. Nothing is sent to the managed service.
Debug records contain fixed statuses, HRESULTs and diagnostic counts, never text,
candidates, titles or document names. Each record is emitted in one call to avoid
interleaving fragments. Raw debugger captures may contain other processes' logs.

Managed protocol checks reject ineligible context text and malformed copies.
Cancellation diagnostics expose only a count. Acceptance failures consume candidates
and never trigger an automatic retry. The native verifier checks the replacement,
caret and bounded neighbors; whole-document integrity is still a manual release gate.

Outstanding security work: production field classification, mutual IPC peer
identity, signed deployment/activation, settings ACL review, real sensitive-field
coverage, secure-desktop/session testing and independent review. No new secure
transport or installer claim is made.
