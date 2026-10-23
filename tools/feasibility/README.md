# Feasibility experiments

## Stage 0.1 implemented

`Stage0/PCWord.Stage0.csproj` is the controlled editor. It proves only the editor's own WinForms API behavior once built and manually exercised.

## Follow-on experiments (not implemented)

1. **UI Automation probe:** inspect Text/Text2 patterns, supported text ranges, selection/caret and read-only state for specific applications. Do not dump document content; report capability metadata and a short bounded test string only in the tester's local UI.
2. **TSF probe:** create a minimal text service or native test client for context, selection/caret, edit-session insertion and registration lifecycle. This is a separate native/API investigation, not a stub that pretends to work.
3. **Keyboard observation probe:** if justified, observe only event metadata in a short-lived opt-in test app; do not store key values/text. A hook alone cannot supply surrounding text or reliable coordinates.
4. **Overlay probe:** test a small `WS_EX_NOACTIVATE` window and verify focus remains in the editor; independently measure caret geometry.
5. Test Notepad, browser, Office, VS Code, and terminal individually and update `docs/compatibility-matrix.md` with versions and evidence.

No follow-on experiment is currently implemented or verified. Stage 0 and the architecture gate remain open.
