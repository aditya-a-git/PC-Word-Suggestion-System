# Codex Task: Implement the Remaining Windows Input Integration

You are working in `aditya-a-git/PC-Word-Suggestion-System`. Implement the remaining Windows integration work end-to-end in the existing repository. Do not stop at research, feasibility notes, TODOs, interfaces without implementations, or a plan. The goal is to deliver the best safe, maintainable implementation that the current architecture and Windows APIs support.

## Workflow and Git requirements

- Read `context.md`, `plan.md`, `README.md`, `docs/windows-integration-status.md`, `docs/architecture-decisions.md`, `docs/privacy-model.md`, `docs/compatibility-matrix.md`, `docs/tsf-feasibility.md`, the testing guide, and all relevant projects/source/tests before editing.
- Work directly on `main`. Do not create or switch to a branch, do not open a PR, and do not rewrite history.
- Inspect the current HEAD and working tree first. Preserve all existing/user changes; never reset, clean, force-push, or overwrite unrelated work.
- Implement, review your diff, run whatever non-interactive builds and automated tests are available, and commit the finished changes directly to `main` with a descriptive message.
- Do not claim runtime compatibility merely because code compiles. We will run the Windows interactive checks separately afterward. Report exactly which commands ran and which tests remain for manual verification.
- If one part cannot be safely implemented with the current APIs or available build environment, implement the safe parts, document the precise blocker and a real fallback, and continue with the rest. Do not stop the whole task just because one API path is difficult.

## Current known state and blockers

The repository currently contains a .NET controlled WinForms editor and a native C++ Text Services Framework (TSF) feasibility probe. The probe is not a production system-wide adapter. The last recorded TSF property lookup failure is `ITfReadOnlyProperty::GetValue(GUID_PROP_INPUTSCOPE)` returning `E_FAIL`; the current status document says this remains unresolved. The context-read path is deliberately fail-closed and requires explicit opt-in plus positive scope classification. InputScope and UI Automation metadata are advisory, not guarantees that content is non-sensitive.

The repository also has context/edit contracts, a shared edit validator, a controlled-editor suggestion surface, native input-scope policy tests, and documentation. Inspect the actual current code; this prompt may not reflect every change already present.

## Required outcomes

### 1. Diagnose and fix the TSF `GetValue` failure where possible

- Trace the complete property lookup and COM/TSF lifetime, edit-cookie, range, variant, and HRESULT flow in `TsfProbe.cpp`. Check official Microsoft API documentation and relevant SDK headers before changing assumptions.
- Determine whether `GUID_PROP_INPUTSCOPE` is being queried through the correct API and whether the property is expected to be exposed in the contexts being tested. Verify interface ownership, `VariantInit/VariantClear`, COM apartment/thread requirements, and the validity of the range and edit cookie.
- Improve diagnostics so each failed operation is identifiable by method and HRESULT, without logging typed text or other content.
- Fix a demonstrated code/API misuse if evidence supports it. Do not suppress `E_FAIL`, reinterpret failure as a safe field, or guess a scope.
- Handle `S_FALSE`, missing property, malformed VARIANT, unsupported interface, empty scope array, unknown values, and all failures as Unknown/denied.
- If a host legitimately does not expose InputScope, document that fact and use another independently validated safety signal where available; do not treat absence of metadata as permission to read.
- Keep the explicit diagnostic opt-in. It must never override a sensitive or unknown classification.
- Add regression tests for the classifier and failure/unknown policy. If the actual COM property behavior cannot be unit-tested without an interactive host, provide a reproducible manual test procedure and clearly mark it as not yet run.

### 2. Implement the production input adapter and connect it to .NET

- Inspect and reuse the existing .NET input contracts, context snapshot/version model, text edit request/result types, predictor API, and cancellation/staleness handling. Do not create parallel abstractions that duplicate existing contracts.
- Implement a real production adapter boundary connecting the supported Windows input integration to the .NET pipeline. Choose the most maintainable mechanism supported by this repo (for example, a native TSF text service with a narrow, versioned IPC/interop boundary if required). Explain the choice in an ADR.
- The adapter must deliver bounded snapshots with field identity, focus, caret/selection state, composition state, sensitivity/classification state, and a monotonically changing context version where available. Represent unavailable information explicitly; never invent context or field identity.
- Bound all context and message sizes. Keep only the minimum required context in memory, clear/invalidate on focus/field changes, and avoid persisting text.
- Handle rapid typing, backspace, selection, punctuation, caret movement, focus changes, adapter shutdown/restart, malformed messages, timeouts, and stale asynchronous results.
- Do not use a global keyboard hook as a substitute for text-context access. Do not poll entire documents, scrape unrelated UI, or silently add broad process access.
- Ensure IPC is authenticated/limited to the current user/session as appropriate, validates all input, and cannot be used to inject arbitrary edits.
- Integrate adapter startup/shutdown and error states into the existing application lifecycle. Unsupported contexts must fail closed and leave ordinary typing unaffected.

### 3. Implement safe explicit cross-process text acceptance

- Implement an actual edit path for supported TSF contexts using the documented TSF edit-session model, including `ITfContext::RequestEditSession` and an authorized read/write edit session where appropriate.
- Do not modify text from a read-only callback or assume that a preflight validator itself performs an edit.
- Immediately before an edit, revalidate context version, field identity, focus, selection/caret, composition state, expected current text/prefix, candidate provenance, and sensitivity status. Reject stale or mismatched requests.
- Restrict edits to the exact intended word/range and candidate; never replace a whole document or arbitrary selection.
- Return a structured result distinguishing accepted, refused, unsupported, stale, timed out, and failed operations. Verify the resulting text/caret when the host permits it; never report success unless success is established.
- Prevent feedback loops from assistant-originated edits and ensure failed or cancelled edits do not leave partially applied state.
- Keep the existing controlled-editor edit path working. Do not remove its safeguards.

### 4. Implement the system-wide suggestion popup for supported contexts

- Build on the existing popup/UI framework if suitable; avoid a second competing UI architecture.
- Implement a Windows popup that is non-activating during normal typing, positions relative to verified caret geometry, stays within monitor/work-area bounds, handles DPI and multi-monitor coordinates, and hides or repositions on focus/caret/context changes.
- Do not let popup display steal focus from the target application. Candidate acceptance must use the explicitly designed input/edit path, not focus-stealing tricks.
- Implement candidate selection, explicit acceptance, dismissal, no-result behavior, stale-result suppression, and clean handling when caret geometry is unavailable.
- Keep the controlled editor as a supported fallback. Do not claim a foreign application is supported unless its context, caret, popup, and edit behavior have been verified.
- If some app categories cannot be supported reliably, make that limitation explicit and ensure the popup stays hidden there.

### 5. Connect input updates to the existing completion pipeline

- Connect supported context snapshots to the existing dictionary-completion/prediction pipeline and deliver results to the popup.
- Do not implement Stage 4's contextual next-word prediction or introduce a new language model as part of this task. This task is the Windows integration foundation, not the next prediction stage.
- Cancel or discard results for older context versions; limit output to the existing candidate contract and preserve the latency-sensitive UI path.
- Ensure disabled, sensitive, unknown, composing, selected-text, or unsupported states suppress suggestions and acceptance according to the current product policy.

### 6. Privacy, input safety, and failure handling

- Fail closed for password, private, PIN, unknown, missing, ambiguous, malformed, and failed field metadata. Include all sensitive scope constants supported by the current policy, including numeric/alphanumeric password and PIN variants where defined by the SDK/documentation.
- Treat InputScope/UIA information as hints, not proof of privacy. Never infer a field is safe solely because it lacks a password flag.
- No typed content in logs, telemetry, crash diagnostics, test output, or persistent storage. Log metadata-only diagnostics such as operation, HRESULT/status, field category, bounded length, and version.
- Require explicit user control for enabling cross-application integration/context reading. The diagnostic environment opt-in must not accidentally become the production consent mechanism.
- Provide reliable global pause/disable and a safe state if the adapter fails or is disconnected, reusing existing controls if present.
- Do not install a keyboard hook, elevate privileges, or register machine-wide components unless demonstrably necessary and explicitly justified. Prefer per-user registration and include safe registration/unregistration and cleanup paths.

### 7. Automated tests and Windows test harness

Add meaningful tests for the integration boundary and edit contract, including as many of these as applicable:
- normal text scope and each known sensitive scope;
- unknown/missing/malformed metadata and HRESULT failures;
- explicit opt-in versus production enable/consent;
- bounded context lengths and malformed IPC messages;
- context version increments and stale requests;
- focus/field identity changes;
- typing/backspace/punctuation/selection/caret movement;
- composition/IME suppression and resume after composition;
- unsupported caret geometry and monitor/DPI positioning;
- candidate provenance and stale prediction responses;
- edit accepted/refused/unsupported/timed-out/failed outcomes;
- no edit on sensitive or unknown fields;
- regression tests proving the controlled editor still behaves correctly.

Use test doubles for platform interfaces where practical. Keep Windows interactive/manual tests separate from deterministic unit tests. Do not add fake tests that merely assert constants or always return success.

### 8. Documentation and architecture

Update the relevant existing documentation rather than creating duplicate status documents:
- `docs/windows-integration-status.md`: implemented vs unimplemented vs not yet runtime-tested.
- `docs/testing-guide.md`: exact build/test commands plus a numbered manual Windows verification procedure using only synthetic text.
- `docs/compatibility-matrix.md`: distinguish tested, partially supported, unsupported, and untested; do not claim results that have not been observed.
- `docs/privacy-model.md`: context retention, consent, unknown fields, diagnostics, and failure behavior.
- `docs/architecture-decisions.md`: document the adapter/IPC choice, TSF edit-session strategy, popup technology, and known limitations.
- `README.md`: accurate current capabilities and instructions.
- Add/update an ADR only if that matches the repository's existing conventions.

Keep API claims grounded in official Microsoft documentation. Avoid copying large sections of documentation.

## Build and test expectations

- Inspect repository build workflows and use the available Windows/.NET/CMake toolchains. Run all feasible managed builds/tests and native configure/build/CTest commands.
- If the current environment is not Windows and cannot compile/run the native TSF component, do not pretend it was tested. Still perform available static review and managed tests, and give the exact commands to run on Windows later.
- Fix compile/test failures caused by your changes where feasible.
- Do not make broad unrelated refactors or start Stage 4 contextual prediction.
- We intend to manually test the Windows integration one behavior at a time after this implementation. Therefore, make the code and tests ready for that verification; clearly separate what is implemented from what is proven at runtime.

## Definition of done

The task is complete only when:
1. The production adapter and .NET integration are implemented, or a concrete API/platform blocker is documented with the safest usable fallback.
2. The TSF property failure is fixed if a supported cause is found; otherwise diagnostics and fallback behavior are precise and fail-closed.
3. Explicit cross-process acceptance uses an authorized edit session and validates freshness and target identity.
4. The popup and prediction flow are implemented for the contexts the adapter can actually support.
5. Tests cover privacy, stale context, edit validation, and failure paths.
6. Documentation accurately distinguishes implementation from runtime proof.
7. Available builds/tests are run and their actual results reported.
8. All changes are reviewed and committed directly to `main`, without creating a branch or PR.

## Final report

After committing, report:
- commit SHA and message;
- files/components added or modified;
- which of the five major integration outcomes are implemented;
- build/test commands actually run and their outcomes;
- Windows manual checks still needed, in a sensible order;
- known limitations and whether Stage 4 is still gated.

Do not mark the Stage 4 gate as passed merely because the integration code was written or compiled.
