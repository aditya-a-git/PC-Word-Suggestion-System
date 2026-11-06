# TSF feasibility spike

## Scope and conclusion

`tools/PCWord.TsfProbe` is an experimental native C++ COM text service. It is separate from prediction and UI Automation code. It observes service activation, document focus, context edit notifications, selection/caret status, caret geometry, InputScope retrieval and the configured safety decision. A status-only DebugView marker can be aggregated by `PCWord.CompatReport`.

The probe performs an opt-in bounded **read attempt** only inside TSF's authorized edit callback, after policy gates. It logs neither text nor the read character counts in the updated design plan? Current implementation emits before/after character counts in status records, not content. This is limited diagnostic metadata. No arbitrary document read, file logging, external IPC, keystroke simulation, edit session, text replacement, suggestion UI, or production adapter is implemented.

**Feasibility is not established for external editing.** The repository has no `ITfEditSession` implementation and no `ITfContext::RequestEditSession` write request. No real Windows GUI host test result with versioned environment and saved output is currently checked in. A DLL build and policy unit tests do not prove host activation, text-store coverage, or document integrity.

## Interfaces and mechanisms implemented

- `ITfTextInputProcessor`: service activation/deactivation.
- `ITfThreadMgrEventSink`: document manager focus and context push/pop notifications.
- `ITfTextEditSink`: `OnEndEdit` callback used with the TSF-provided read cookie.
- `ITfContext::GetSelection`, `ITfRange::IsEmpty`, and `ITfContext::GetActiveView` / `ITfContextView::GetTextExt`: inspect whether selection is collapsed and whether caret geometry is available.
- `ITfContext::GetAppProperty(GUID_PROP_INPUTSCOPE)`, `ITfReadOnlyProperty::GetValue`, then `IEnumTfPropertyValue::Next`: classify declared sensitive InputScope when the host exposes it.
- `ITfRange::Clone`, `Collapse`, `ShiftStart` / `ShiftEnd`, and `GetText`: bounded diagnostic reads capped to 128 UTF-16 units before and 32 after the caret. Read is denied unless all settings, executable allowlist, context association, collapsed selection, non-composition, caret geometry, known safe scope or explicitly gated unknown scope, and opt-in checks pass.
- `ITfSource::AdviseSink` / `UnadviseSink`: bind and release the focused context's edit sink.
- COM registration uses an in-process class factory and HKCU COM registry entries; TSF profile registration uses `ITfInputProcessorProfiles`.

No write edit-session contract is exercised. A production design would need a short-lived operation object implementing `ITfEditSession`, `RequestEditSession` with appropriate read/write flags, and a recheck inside `DoEditSession` of context identity, monotonic operation generation, expected range text and selection before changing the document. Post-edit selection/text verification must happen within a safe TSF callback. This is a future experiment, not current behavior.

## Safety and privacy

- `HKCU\Software\PCWord` settings must parse successfully and `Enabled=1`; malformed/missing values deny access.
- The executable name must exactly match an explicit allowlist entry. Defaults are disabled with `notepad.exe` as the only allowlisted name.
- `PCWORD_TSF_ALLOW_CONTEXT_READ=1` is additionally required for diagnostic reads. It is opt-in, not enabled by normal application startup.
- Sensitive InputScope classifications deny. Unknown/unavailable scope is not proof of safety; in the probe it may pass only for the explicitly allowlisted host and all other gates. Keep the allowlist narrow and synthetic text only.
- InputScope is host-declared and not a complete sensitivity signal. Do not allowlist browsers by default.
- The callback uses the TSF cookie; it does not poll or retain arbitrary text. It reports status and lengths only. Text is transient in a bounded local buffer for the duration of the callback.
- Disabling or changing settings is re-read on each edit notification. There is no pending asynchronous write operation in this diagnostic probe.
- No external write path exists, so disabling cannot race a queued edit; a future writer must enforce a generation/identity check inside its edit session.

## Registration

The DLL's `DllRegisterServer` writes COM class values and TSF profile to the current user's registry. Unregistration removes that class/profile. The documented build output is x64; use matching `System32\regsvr32.exe` from a 64-bit session and the same Windows user/DLL for both operations:

```powershell
$dll = (Resolve-Path .\PCWordTsfProbe.dll).Path
& "$env:WINDIR\System32\regsvr32.exe" "$dll"
& "$env:WINDIR\System32\regsvr32.exe" /u "$dll"
```

Do not use `/i`, machine-wide registration, or modify unrelated settings. The repository contains an owner-reported non-elevated registration failure at `ITfInputProcessorProfiles::Register` (`0x80004005`) and elevated success; it is not independently reproduced. The exact privilege requirement remains unknown. Prefer a disposable VM/test account and non-elevated registration first. Verify cleanup in HKCU after unregistering.

## Runtime evidence

| Evidence | Result |
|---|---|
| Native x64 DLL build in GitHub Actions | Passed on run 42220760078 |
| Native policy tests | Passed 2/2 on run 42220760078 |
| Managed policy/build checks | Passed in same CI run |
| TSF service registration/activation on this turn | Not run; this environment has no interactive Windows test host |
| Notepad/WordPad/Word/browser/VS Code context acquisition | No dated, versioned result available |
| Authorized external edit session | Not implemented; untested |
| Focus/lifecycle race behavior in a live app | Not run |

A previous InputScope `ITfReadOnlyProperty::GetValue` `E_FAIL` observation is documented as owner-reported/host-specific. It remains unresolved. No result is generalized across application versions.

## Required next experiment

Use `docs/testing-guide.md` in a disposable Windows VM. Capture DebugView marker lines locally; do not include unrelated logs in source control. Save only structured status fields in a JSON report. For each app and control, record app/version, Windows build, process architecture, service activation, focus/edit events, selection and caret booleans, read decision/reason and HRESULT. Use synthetic text and never record its content. Then implement and separately validate a TSF write-session host only if bounded read behavior and stale-focus invalidation are demonstrated first.

