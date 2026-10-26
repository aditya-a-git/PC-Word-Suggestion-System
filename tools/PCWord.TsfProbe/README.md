# PCWord TSF feasibility probe

Experimental native C++ COM text service for evaluating Windows Text Services Framework against the existing UI Automation probe. It is not production-ready.

Build on Windows with Visual Studio C++ tools and CMake 3.24+:

```powershell
cmake -S .\tools\PCWord.TsfProbe -B .\tools\PCWord.TsfProbe\build -A x64
cmake --build .\tools\PCWord.TsfProbe\build --config Release
```

The DLL reports activation, focus/edit notifications, selection availability, caret rectangle availability, and bounded character counts to the debugger output. It never prints text content or edits documents. Context reads are disabled by default and require `PCWORD_TSF_ALLOW_CONTEXT_READ=1` in the target application's environment. Use only synthetic text. Unknown or password/PIN input scopes must fail closed.

This is a feasibility spike only: no key interception, popup, prediction, or text replacement. Registration and runtime testing must be performed manually on a disposable test environment; follow the Windows TSF test matrix in `docs/tsf-feasibility.md`.