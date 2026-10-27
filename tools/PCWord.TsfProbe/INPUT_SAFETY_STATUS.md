# TSF input safety status

## Current behavior

The probe receives TSF focus/edit notifications and can obtain caret geometry in the observed hosts. It does **not** log text content.

Context reads require both:
- explicit opt-in via `PCWORD_TSF_ALLOW_CONTEXT_READ=1` in the host process environment; and
- a positively observed `InputScopeStatus::NonSensitive` classification.

The safety policy is isolated in `InputSafetyPolicy.h`. `Sensitive` and `Unknown` both deny context reads. Failed property access, failed interface queries, empty scope metadata, and unrecognized values must remain `Unknown`.

## Current blocker

The latest captured DebugView log shows `GUID_PROP_INPUTSCOPE GetValue` returning `0x80004005` (`E_FAIL`) during edit notifications. The resulting classification is unknown and context reads are skipped. The same log reports `opt-in=no`, so it does not yet verify that the test host inherited the opt-in environment variable.

This is an unresolved TSF property-access issue, not evidence that text-context reads are working. Do not relax the fail-closed rule or use text reads in unknown/sensitive fields to get around it.

## Next verification

1. Build the DLL after closing any process that has loaded it.
2. Start a fresh test host from a shell that has `PCWORD_TSF_ALLOW_CONTEXT_READ=1` set.
3. Confirm the log reports `opt-in=yes`.
4. Diagnose the TSF input-scope property retrieval separately. Keep reads denied until a safe classification is positively established.
5. After that succeeds, verify context acquisition only in a controlled non-sensitive editor and verify password-field behavior remains blocked.

## Scope of this milestone

This refactor makes the privacy decision explicit and independently reviewable. It does not claim that the TSF property lookup is fixed, nor that the DLL has been rebuilt or tested on the developer machine.
