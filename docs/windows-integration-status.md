# Windows integration implementation status

## Scope

This document tracks the Windows-facing work before Stage 4 (contextual next-word prediction). The repository has a controlled WinForms editor and a native TSF feasibility probe, but those are not yet a production system-wide typing assistant.

## Implemented in code

- A native per-user COM/TSF feasibility service with registration and unregister support.
- TSF activation, document focus, edit notification, selection/caret geometry reporting.
- Bounded context-count probe behind explicit process-environment opt-in.
- A fail-closed input-scope policy: context reads require opt-in and a positive non-sensitive classification.
- Explicit blocking for Windows input scopes marked Password, NumericPin, AlphanumericPin, NumericPassword, AlphanumericPassword, or Private.
- Compile-time tests for the opt-in policy and sensitive-scope classifier, registered with CTest.
- A separate UI Automation probe that reports metadata and, only with an explicit flag, bounded context lengths for synthetic test text.
- A controlled-editor suggestion surface with explicit keyboard acceptance, dismissal, and stale-context checks.

## Not implemented / not yet proven

- A production cross-process IInputAdapter connecting TSF context to the .NET prediction pipeline.
- A production system-wide non-activating popup positioned at foreign caret coordinates.
- A safe, verified cross-process edit/acceptance path.
- Reliable sensitivity detection for every app/control. InputScope and UI Automation metadata are hints and may be absent or incorrect.
- IME/composition support, per-app exclusions, global pause/status controls, and application lifecycle/settings integration.
- Runtime compatibility across Notepad, browsers, Office, VS Code, terminals, elevated applications, remote sessions, and custom-rendered controls.
- A Windows runtime pass proving that ITfReadOnlyProperty::GetValue succeeds in supported hosts. The recorded E_FAIL remains unresolved.

## Research findings

Microsoft documents ITfContext::GetAppProperty(GUID_PROP_INPUTSCOPE, ...) followed by ITfReadOnlyProperty::GetValue(...) and querying the returned interface for ITfInputScope. The property API can return S_FALSE when the context owner does not support a property and E_FAIL for an unspecified failure. Therefore, a failed lookup is not evidence of a safe field and must remain Unknown.

Microsoft also documents ITfContext::RequestEditSession for obtaining read or read/write access. Text modifications must be performed in an authorized edit session; a read-only callback is not a valid place to mutate text. A future acceptance implementation must verify context version, field identity, expected text and selection immediately before requesting a write session, and must report failure when the host refuses the operation.

Sources:
- [ITfInputScope](https://learn.microsoft.com/en-us/windows/win32/api/inputscope/nn-inputscope-itfinputscope)
- [ITfContext::GetAppProperty](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfcontext-getappproperty)
- [ITfContext::RequestEditSession](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfcontext-requesteditsession)
- [ITfInsertAtSelection::InsertTextAtSelection](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfinsertatselection-inserttextatselection)

## Required verification sequence

1. Build the TSF probe with the Windows SDK and run CTest.
2. Register it in a disposable Windows test account and confirm activation/focus/edit notifications.
3. Reproduce or resolve GetValue failure using synthetic text. Keep context reads disabled until scope is positively classified.
4. Verify Password, NumericPin, AlphanumericPin, NumericPassword, AlphanumericPassword and Private scopes are blocked; unknown scope must also be blocked.
5. Test the UI Automation probe separately across the target application matrix. Record only capabilities/status, never typed text.
6. Design and implement the production adapter only after the evidence establishes which fields expose stable context and caret geometry.
7. Implement cross-process popup and explicit edit acceptance with version/field validation, then run regression tests for dropped/duplicated characters and focus theft.
8. Update the compatibility matrix from actual runs, not API assumptions.

## Stage 4 gate

Do not begin contextual prediction until Windows integration can produce a versioned, bounded, known-safe context in the supported test editor and can perform an explicit, verified accepted edit without losing or duplicating text. Compilation alone does not satisfy this gate.
