# TSF feasibility spike

The native probe in `tools/PCWord.TsfProbe` is an experimental C++ COM text service that compares TSF lifecycle/context availability with the existing UI Automation probe.

## Scope

- Observes service activation, document focus, edit notifications, collapsed-selection availability, and caret rectangle availability.
- Bounded surrounding-text counts are behind an explicit target-process environment opt-in.
- Text content is not emitted; no text is changed and no keystrokes are synthesized.
- This is not a production adapter and has no prediction or suggestion UI.

## Build

On Windows with Visual Studio C++ tools and CMake:

```powershell
cmake -S .\tools\PCWord.TsfProbe -B .\tools\PCWord.TsfProbe\build -A x64
cmake --build .\tools\PCWord.TsfProbe\build --config Release
```

The DLL must be registered as a per-user TSF profile and enabled manually for a test account before it will receive notifications. See `tools/PCWord.TsfProbe/README.md`. Runtime validation is still pending.

## Manual comparison

Use synthetic text only. Test each field separately: Notepad, ordinary browser input, browser contenteditable, VS Code editor, and LeetCode editor. Record only app/version, field type, activation, context availability, collapsed selection, caret rectangle, bounded counts, and pass/fail. Never copy typed text into logs or notes.

Password/PIN fields must not be read. If input sensitivity cannot be determined reliably, treat context as unavailable. TSF input scope is not a universal sensitivity guarantee, and a field's provider may not expose TSF context at all.

## Decision gate

Do not integrate TSF into the prediction engine until Windows runtime tests establish field coverage, stable caret context, and reliable invalidation on focus/context changes. A separate edit-session experiment must verify stale-context rejection and no duplicated/lost characters before replacement is considered. Benchmarking remains deferred to the final stage.
