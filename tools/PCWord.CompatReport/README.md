# PCWord compatibility report

Reads a saved DebugView capture in tab-separated sequence, elapsed time, PID and message format and writes aggregate Markdown rows only:

```powershell
dotnet run --project .\\tools\\PCWord.CompatReport\\PCWord.CompatReport.csproj --configuration Release -- <capture.txt> "App name" "Exact version" "Field category"
```

The parser accepts the TSF probe's two-line `[PCWord.TSF] ` prefix/message records, groups by PID, and retains only fixed status events and aggregate counters. It ignores unrelated output and never includes raw messages in its output. Treat raw captures as potentially sensitive: they may contain unrelated process output. Keep them only in the ignored local `logs/` folder and do not commit or copy them into documentation. The report does not prove safe text insertion or popup behavior.
