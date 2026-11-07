# Settings and privacy

## Current implementation

The settings model lives in `PCWord.Core.UserSettings`, persistence in `IUserSettingsStore` / `JsonUserSettingsStore`, and the application-facing service in `UserSettingsService`. The Stage 0 WinForms application opens a dedicated Settings window and consumes the service's change notifications.

For every update, the service validates the model, saves it, updates its in-memory snapshot, then synchronously notifies runtime subscribers. The controlled-editor app applies the enabled state to `DictionaryCompletionCoordinator`; disabling also hides the popup. A failed save leaves the previous state active. If a runtime notification throws, the service attempts to restore the persisted and in-memory snapshot and notifies subscribers of the rollback.

Settings files contain only configuration. The current model contains:

| Setting | Default | Runtime behavior |
| --- | --- | --- |
| Enabled | false | Enables/disables the controlled-editor suggestion coordinator; disabling clears pending suggestions and hides the popup. |
| Pause shortcut | Ctrl+Alt+Shift+P | Persisted and validated; the current app's global pause shortcut remains a fixed implementation value. It is not yet configurable at runtime. |
| Acceptance shortcut | Ctrl+Alt+Enter | Persisted and validated; current acceptance keys remain fixed in the editor. It is not yet configurable at runtime. |
| Host allowlist | `notepad.exe` | Persisted executable-name list. It does not authorize production external-app prediction because there is no production external input adapter. |
| Personalization enabled | false | Retained for compatibility; personalization is not implemented and the UI does not expose this option. |

The Settings window provides General, Typing & Suggestions, Applications, and Privacy tabs. Unsupported settings are described as unavailable rather than presented as active controls.

## Persistence, validation, and migration

The application stores JSON at `%LOCALAPPDATA%\PCWord\settings.json`. The existing JSON store writes to a temporary file and replaces the destination, ignores unknown properties, rejects unsupported schema versions and invalid values, and returns safe defaults for a missing or malformed file. Saves validate before writing.

The current schema version remains 1. Existing JSON settings remain readable. The separate registry-backed diagnostic TSF settings have **not** been migrated; they remain a distinct diagnostic-only policy source. Do not interpret the JSON host names as a production allowlist or as permission to read arbitrary fields. A future migration must validate the legacy values, persist and verify the JSON copy, then retain the registry values until migration is confirmed.

## Privacy behavior and limits

- The only live prediction path established here is the application's own controlled editor.
- The input contract rejects sensitive, unavailable, selected, unfocused, and composing contexts. A user setting cannot override those checks.
- The TSF component remains diagnostic-only. External context reading, external popup delivery, and external text acceptance are not production behaviors.
- Password-field access is not offered. The settings UI explicitly states that no option enables it.
- Settings and diagnostic messages do not contain typed content. Personalization and anonymous metrics are not available as configurable runtime features.

## Verification status

Automated tests cover existing model/store validation and serialization plus settings-service persistence order, notifications, defensive snapshots, invalid updates, and no-op updates. These tests do not prove real WinForms behavior, registry migration, global shortcut registration, external-app allowlisting, or TSF privacy enforcement. Run the managed solution tests from `docs/testing-guide.md`, then manually verify the checkbox and Settings window in the controlled-editor app.

## Remaining implementation gates

A genuinely unified production settings system still requires a production input adapter with a permission-enforced executable identity, runtime-applicable configurable shortcuts, prediction-mode controls wired into the coordinator, verified legacy settings migration, and repeatable Windows UI/privacy tests. Plan exit criteria remain open until those gates are met.