# Compatibility matrix

Compatibility is recorded per application version and field category. **Observed** means a diagnostic event was seen; it is not production support. **Implemented** applies only to the PCWord-owned editor. Every external text-editing and popup cell remains untested because no production external edit/popup path exists.

| Application/version | Field type | Context access | Caret location | Text insertion | Popup behavior | Sensitive-field behavior | Regression status |
|---|---|---|---|---|---|---|---|
| PCWord owned editor (WinForms; current build) | Owned RichTextBox, non-sensitive synthetic content | Implemented: bounded in-process snapshot (128 units before, 32 after); covered by managed tests | Implemented: owned caret state and geometry | Implemented: explicit prefix replacement with verification in code/tests | Implemented in owned form; managed behavior covered, live UI not run in this compatibility session | Implemented sensitive toggle denies collection and edits; automated coverage | Implemented; managed tests reported in testing guide |
| Notepad/basic Win32 | Owner reported a non-password text field; exact app version, field details and date not supplied | Observed (probe only): TSF activation/focus/edit notifications reported; InputScope GetValue E_FAIL (0x80004005); no log supplied | Observed (probe only): collapsed selection and caret rectangle reported by owner; no log supplied | Untested; no external edit path | Untested; no external popup | Password fields in browser reportedly detected by owner, without log; not evidence for this Notepad field | Untested; no dated/versioned record |
| Packaged Notepad | Basic text input | Untested | Untested | Untested | Untested | Untested | Untested |
| Browser text input | Non-password text input | Untested | Untested | Untested | Untested | Untested | Untested |
| Browser contenteditable | Editable web content | Untested | Untested | Untested | Untested | Untested | Untested |
| Browser password field | Password input | Untested; owner reports browser password detection, no log in repo | Untested | Untested | Untested | Reported by owner; no log in repo | Untested |
| Word or similar office editor | Document body text | Untested | Untested | Untested | Untested | Untested | Untested |
| VS Code | Editor text buffer | Untested | Untested | Untested | Untested | Untested | Untested |
| VS Code | Integrated terminal | Untested | Untested | Untested | Untested | Untested | Untested |
| Windows Terminal | Terminal input | Untested | Untested | Untested | Untested | Untested | Untested |
| Messaging application | Compose field | Untested | Untested | Untested | Untested | Untested | Untested |
| XAML/UWP application | Text box | Untested | Untested | Untested | Untested | Untested | Untested |
| Elevated application | Text field | Untested | Untested | Untested | Untested | Untested | Untested |
| Remote Desktop | Remote session text field | Untested | Untested | Untested | Untested | Untested | Untested |
| Game or custom-rendered application | Custom text field | Untested | Untested | Untested | Untested | Untested | Untested |

## Evidence and policy

The Notepad row is based only on the owner's undated report in the task request. No app version, precise field subcategory, saved probe log, or test date was supplied, so it is not a reproducible compatibility result. Browser password detection is likewise only owner-reported and has no log in the repository.

Raw DebugView files must remain local and ignored. Add only generated aggregate rows to this table, and record application version, exact field category and test date. Never copy typed text, password contents, prefixes, candidates, titles or unrelated log output into documentation.

All external applications remain unsupported for production prediction. A TSF diagnostic event does not prove safe context, edits, or popup support. A row may not be generalized to another version or field category.

### Conservative defaults

- **Password/sensitive fields:** deny context reads and editing. Any known sensitive classification is denied.
- **Unknown sensitivity:** deny by default. Diagnostic reads require the existing explicit opt-in, enabled setting, structural gates, and exact host allowlist; that exception is for observation only and is not production support.
- **Elevated applications:** unsupported/denied. TSF/UI isolation restricts interaction across integrity levels; the assistant intentionally does not run elevated.
- **Remote desktop:** unsupported/denied unless separately tested and explicitly allowlisted; remote field identity and safe edits are not established.
- **Games/custom-rendered editors:** unsupported/denied; text and caret semantics may be unavailable.
- **Terminals:** unsupported/denied; command-line content may be sensitive, and no terminal edit path is implemented.

Changing adapters, policy, or popup behavior requires rerunning every matrix row that could be affected, using the guide's synthetic-data script and recording per-version/per-field results.

## Controlled TSF runtime verification (2026-11-06)

No application was launched or tested in an interactive Windows TSF session during this feasibility pass. CI builds the diagnostic DLL and runs policy unit tests only. The older owner-reported Notepad/basic Win32 and browser observations remain unverified because no app version, Windows build, date, or retained status report is available.

| Application | Version / Windows build | Architecture | TSF activation | Context / selection / caret | Authorized edit | Result |
|---|---|---|---|---|---|---|
| Notepad | Not run in this pass | n/a | Untested | Untested | Not implemented | Unsupported pending runtime test |
| WordPad | Not run; availability not checked | n/a | Untested | Untested | Not implemented | Untested |
| Microsoft Word | Not run | n/a | Untested | Untested | Not implemented | Untested |
| Chrome / Edge | Not run | n/a | Untested | Untested | Not implemented | Untested |
| Visual Studio Code | Not run | n/a | Untested | Untested | Not implemented | Untested |
| PCWord owned WinForms editor | Current repository build | x64 CI build; interactive run not performed | Not applicable (in-process adapter) | Implemented/tested under managed contracts | Owned-control explicit edit path only; live GUI not run | Managed coverage only |

The TSF probe has no write edit-session implementation. Do not infer text-edit support from TSF activation or diagnostic read results.


## System-wide popup verification (2026-11-06)

No interactive UI run was performed. The UI project compiles in CI, but external caret delivery and TSF acceptance are not connected.

| Application | Popup activation/focus | Caret placement | DPI/multi-monitor | Mouse/keyboard acceptance | Result |
|---|---|---|---|---|---|
| PCWord controlled editor | Existing child popup only; separate top-level UI not integrated | Owned child control only | No test | Existing editor shortcuts; not the new popup | Existing controlled-editor behavior only |
| Notepad | Untested; production adapter unavailable | Untested | Untested | Untested; authorized external edit path absent | No system-wide support claim |
| WordPad / Microsoft Word | Untested | Untested | Untested | Untested | Untested |
| Chrome / Edge | Untested | Untested | Untested | Untested | Untested |
| Visual Studio Code | Untested | Untested | Untested | Untested | Untested |

The pure geometry tests are algorithm tests only. They do not test an HWND, focus preservation, real caret coordinate transforms, monitor DPI, or app compatibility.
