# Uninstallation

## Status

There is no PCWord installer or uninstaller. The normal controlled-editor app does not install a Windows service or production TSF profile. These steps describe cleaning up a development run only; they are not evidence of a verified product uninstallation.

If using the portable evaluation ZIP, close `PCWord.Stage0.exe` and remove only the extracted evaluation folder. The ZIP does not install or register a TSF component. Preserve `%LOCALAPPDATA%\PCWord\settings.json` and local model data unless you explicitly choose to remove them.

## Close the development app

1. Close the PCWord controlled-editor window.
2. In Task Manager, confirm the `PCWord.Stage0` process has exited. If it remains, end only that PCWord process.
3. If you built from source and want to remove the development copy, preserve any files you need, then remove that checkout using your normal source-control cleanup procedure. Build output can be regenerated.

Do not remove `%LOCALAPPDATA%\PCWord\settings.json` or any local model/data unless you explicitly want to delete that user data. No cleanup tool currently distinguishes obsolete PCWord state from data you may want to retain.

## If the diagnostic TSF probe was manually registered

Only if you followed the TSF compatibility experiment in [testing-guide.md](testing-guide.md):

1. Use the exact DLL path and the same Windows account/registration context used for the experiment.
2. Follow the unregister command and cleanup/restore steps in that guide.
3. Switch back to the previous input method and verify Windows text input works normally.
4. Confirm that the diagnostic DLL is no longer loaded by test applications.

Do not delete broad registry keys or modify unrelated text services. If you cannot identify the exact registration and account used, stop and inspect the experiment notes before changing Windows registration state. This repository has no automated registry cleanup tool.

## Product uninstaller requirements still open

A production uninstaller must stop PCWord-owned processes, unregister only PCWord-owned TSF profiles/components, remove only installer-owned files and shortcuts, preserve settings by default, and verify that normal Windows input still works. No such behavior has been implemented or tested. Reinstallation after removal is not established.