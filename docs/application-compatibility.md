# Application compatibility

No external application is production-supported. No dated, versioned interactive
Windows run was available in this Linux task. Earlier owner observations remain
unverified and are not upgraded by build/test success.

Support levels: Fully supported requires every relevant gate; Partially supported
requires explicitly recorded restrictions; Suggestions only forbids acceptance;
Unsupported means there is no verified production path; Blocked means the intended
privacy/security policy forbids access. Untested is an evidence status, not support.

| Application/category | Version/framework evidence | Context / caret / popup / edit / sensitivity | Status |
|---|---|---|---|
| Windows Notepad | Version/build not recorded; modern and classic variants may differ | All real-host checks pending; synthetic TSF experiment provided | Unsupported, untested |
| Microsoft Word | Version/framework unverified | All pending | Unsupported |
| Microsoft Outlook | Version/framework unverified | All pending | Unsupported |
| Microsoft Excel | Version/framework unverified | All pending | Unsupported |
| Google Chrome | Version unrecorded; browser providers vary by field | All pending; passwords/payment/OTP must deny | Unsupported |
| Microsoft Edge | Version unrecorded; browser providers vary by field | All pending | Unsupported |
| Firefox | Version/framework unverified | All pending | Unsupported |
| Windows Settings/native controls | Control/version unrecorded | All pending | Unsupported |
| Electron (e.g. VS Code) | Version/framework integration unverified | All pending | Unsupported |
| Win32 text controls | Control/provider unverified | All pending | Unsupported |
| WPF text controls | Provider/version unverified | All pending | Unsupported |
| WinForms external apps | Controlled PCWord editor does not prove another host works | All pending | Unsupported |
| Password/credential fields | Host-declared scope is an incomplete signal | Policy tests only; no runtime disclosure test | Blocked |
| Elevated, secure desktop, remote, terminal, custom rendering | Outside current experiment | Not authorized | Blocked/unsupported |

For each real run use [testing.md](testing.md) and the environment report script.
Record Windows edition/build, application and architecture, activation, bounded
read, collapsed/selected caret, edit request/session HRESULTs, post-edit verification,
full synthetic document integrity, focus/DPI and sensitive-field outcomes. A visible
window, activation marker or successful model test is insufficient for support.
