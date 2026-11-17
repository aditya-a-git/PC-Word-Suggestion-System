# Known limitations and release blockers

PCWord is not production-ready. The following remain incomplete:

1. Real Windows/Notepad activation, context acquisition and authorized editing have
   not been demonstrated for the new experiment. The existing owner notes lack
   dated versions and are not verification evidence.
2. The separate experiment replaces only the synthetic `pcword` token. It does not
   provide suggestions, general text editing, selection replacement or empty-document
   insertion. It requests synchronous sessions on explicit keys and rejects denied locks.
3. Verification compares the replacement, caret and a bounded neighboring window.
   It cannot prove that a broken provider changed nothing elsewhere in the document.
4. No native-to-managed pipe, peer authentication, external IInputAdapter, service
   lifecycle/reconnect, external popup keyboard route or production end-to-end flow.
5. InputScope/filename policy is diagnostic only. Unknown scope is allowed in
   explicitly opted-in Notepad experiments; private content in ordinary fields
   cannot be identified reliably. No browser/Office support or security claim.
6. No interactive focus, mixed DPI, negative-monitor, RTL, accessibility, scrolling,
   composition, selection, session or sustained-use evidence. Hosted Windows CI
   runs model tests and compiles code; it is not an interactive application test.
7. Native builds target x64. x86/ARM64, minimum supported Windows version, modern
   Notepad activation/environment inheritance, and registration privileges are unverified.
8. COM registration uses HKCU; the Windows TSF profile/category API storage/privilege
   behavior requires investigation. Do not escalate a failed test registration into
   global installation or treat manual cleanup as a tested uninstaller.
9. No consumer installer, signed package, upgrade/repair/rollback or clean-uninstall
   verification. The CI ZIP is a portable controlled-editor evaluation build.
10. Dictionary/model/repository-wide redistribution review and optional model
    licensing remain release tasks. Existing assets and ranking are preserved.

The supplied requirements forbid progressing to dependent production stages before
the real-host TSF gate is verified. This change implements that experiment and
independent regression fixes; it does not declare the gate passed.
