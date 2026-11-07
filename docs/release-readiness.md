# Release readiness

**Decision: not release-ready.** PCWord is a Windows development prototype. The only implemented typing path is the application's own controlled WinForms editor. It is not a system-wide input assistant and has no supported external applications.

## Environment target

| Area | Evidence-backed status |
| --- | --- |
| Windows | Windows desktop is the target. CI uses `windows-latest`; no minimum Windows build or Windows edition has been validated. Windows 11 is the initial intended target, not a verified product support statement. |
| Architecture | Native TSF probe CI build is x64. No x86 or ARM64 production build is validated. |
| .NET | Projects target .NET 10; a .NET 10 SDK is used in CI. No end-user runtime package or self-contained deployment has been produced. |
| Desktop runtime | WinForms targets Windows Desktop. The exact runtime prerequisite for a redistributed build has not been verified because there is no publish artifact. |
| Privileges | The controlled-editor app has no installer and no elevation requirement established. TSF probe registration is a diagnostic/manual activity and is not part of normal app startup. |
| Services / IPC | No production service, named-pipe transport, or cross-process prediction channel exists. |
| Install scope | No per-user or machine-wide installation is implemented. |

Do not advertise support for a Windows edition, OS build, processor architecture, or installed configuration beyond this development evidence.

## Application compatibility

The controlled editor has automated coverage for bounded context and explicit edits, but its current WinForms window has not been independently exercised as an installed application. External compatibility is unsupported, even when diagnostic TSF events have been observed.

| Application | Classification | Evidence / gap |
| --- | --- | --- |
| PCWord controlled editor | Experimental, code-level behavior | Managed tests cover owned-control contracts. Interactive text-integrity, focus, DPI, and prolonged-use runs are still required. |
| Windows Notepad | Unsupported for prediction | Owner observations are undated and lack exact version/environment artifacts; no production edit or popup path exists. |
| Microsoft Word | Not tested | No production external adapter. |
| Google Chrome | Not tested | No production external adapter; browser fields have mixed sensitivity. |
| Microsoft Edge | Not tested | No production external adapter; browser fields have mixed sensitivity. |
| Visual Studio Code | Not tested | No production external adapter. |
| Other applications | Unsupported / not tested | See the per-field [compatibility matrix](compatibility-matrix.md). |

## Acceptance criteria and current results

| Gate | Acceptance evidence | Status |
| --- | --- | --- |
| Word completion and next-word prediction | Reproducible owned-editor run with both modes, explicit acceptance, punctuation/whitespace and Unicode cases | Partial: implementation and managed tests exist; installed GUI pass is outstanding. |
| System-wide input in declared apps | Per-version/per-field compatibility runs; context, caret, popup, keyboard routing, safe insertion and privacy all pass | Blocked: no production external adapter or authorized TSF write edit session. |
| Settings and pause/resume | Runtime setting change immediately affects coordinator; persistence and restart behavior verified | Code added for controlled editor; latest change set has not been built/tested in this environment. |
| Text integrity | No corruption in repeatable tests covering typing, selection, punctuation, Unicode, undo/redo, stale results | Partial: owned-control automated contracts only; manual lifecycle and undo/redo evidence absent. |
| Privacy | Sensitive fields and disallowed hosts denied at runtime; no content in logs; independent checks | Partial: owned-control policy and native policy unit tests exist; no production external enforcement. |
| Stability | Recorded sustained-use duration and before/after memory, CPU, handles, threads, crashes, latency | Not run. No duration or measurements are claimed. |
| Installation / upgrade / removal | Clean install, upgrade, settings preservation, unregister, uninstall, and reinstall on clean Windows environment | Blocked: no installer, upgrade, or uninstaller exists. |
| Release build | Reproducible, versioned package tested on clean Windows, contents and checksums recorded | Blocked: no package pipeline or release artifact exists. |

No latency, memory, CPU, or stability threshold has been selected from product requirements and representative measurements. The synthetic benchmark in [benchmarks.md](benchmarks.md) is not external-app or sustained-use evidence; it must not be used to imply release performance.

## Security and privacy review

- **IPC / message authorization:** There is a managed protocol and stale-request gate, but no transport or authenticated peer. No external document edit is reachable through it.
- **Process identity / allowlisting:** The diagnostic TSF policy matches executable file names. This is not strong executable identity; a same-name executable from another path may match. It is not a production permission boundary.
- **TSF / COM:** The native component is an experimental probe. There is no authorized write edit session, production registration/activation lifecycle, or installer cleanup logic.
- **Sensitive fields:** The probe's classified sensitive scopes are denied and unknown sensitivity is conservative under policy, but there is no broad production adapter. Never infer safety from a failed/unknown InputScope.
- **Settings:** JSON saves use a temporary file and replacement. File ACLs and installer-created permissions have not been independently reviewed.
- **Models and dictionaries:** The checked-in sample dictionary is synthetic and unsuitable for a release vocabulary. See [data/README.md](../data/README.md) for asset provenance/licensing. No real dictionary is bundled as a production asset.
- **Logging:** The implementation/docs report numeric aggregate metrics and no typed-content logging on the normal path. This is source inspection, not a runtime log-capture audit.
- **Network:** No remote prediction service is part of the design; runtime network monitoring has not been performed as part of release acceptance.

Do not register the probe or expose it as a production input method in a user release.

## Packaging and release pipeline

CI currently restores and builds the .NET solution in Release, runs managed tests, builds the x64 native TSF probe, runs native policy tests, and uploads the probe DLL as a short-retention CI artifact. The uploaded DLL is a diagnostic artifact, not an installer or release package.

No packaging strategy is selected. MSIX, MSI, and a per-user installer must not be chosen until production TSF registration and activation requirements are known. No install, upgrade, uninstall, shortcut, startup, settings-migration, or rollback behavior has been verified. No release artifact, versioned package, release notes, or checksum is produced.

The real-corpus license review, repository-wide license selection, and production dictionary provenance are also open. Do not distribute the sample or development artifacts as a consumer release without resolving those items.

## Release blockers

1. **Critical:** No production external input/edit path; TSF feasibility and text-integrity gate is open.
2. **Critical:** No verified sensitive-field and executable-identity enforcement at an external-app boundary.
3. **High:** No installer, upgrade path, or clean uninstaller.
4. **High:** No supported-app matrix backed by dated versioned runtime evidence.
5. **High:** No sustained-use/resource results or external end-to-end latency measurements.
6. **High:** No release-quality dictionary/model asset and unresolved repository/asset licensing.
7. **Medium:** The new settings window/service changes have not yet been validated by a managed CI run or manual Windows UI pass.
8. **Medium:** Architecture/edition/runtime prerequisites and release acceptance thresholds are not established.

## Required next release gate

First implement and validate the production input adapter and permission boundary in a controlled Windows test environment. Then run the compatibility and privacy matrix, sustained-use tests, and edit-integrity scenarios. Only after these gates pass should packaging, installation, upgrade/uninstallation, and release-artifact generation be selected and verified.