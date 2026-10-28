#include "InputSafetyPolicy.h"
#include <array>
#include <iostream>

#define REQUIRE(condition) do { if (!(condition)) { std::cerr << "failed at line " << __LINE__ << "\n"; return __LINE__; } } while (false)

using namespace pcword::input;

static SafetyDecision Expected(const SafetyInputs& value)
{
    if (!value.settingsValid) return SafetyDecision::DeniedInvalidSettings;
    if (!value.enabled) return SafetyDecision::DeniedPaused;
    if (value.classification == FieldClassification::Sensitive) return SafetyDecision::DeniedSensitive;
    if (!value.focused || !value.collapsedSelection || value.composing || !value.contextAvailable)
        return SafetyDecision::DeniedStructuralState;
    if (!value.hostAllowlisted) return SafetyDecision::DeniedHostNotAllowlisted;
    if (!value.diagnosticOptIn) return SafetyDecision::DeniedDiagnosticOptIn;
    return value.classification == FieldClassification::DeclaredNonSensitive
        ? SafetyDecision::AllowedByDeclaredScope : SafetyDecision::AllowedByHostPolicy;
}

int main()
{
    constexpr std::array<bool, 2> booleans = { false, true };
    constexpr std::array<FieldClassification, 3> classifications = {
        FieldClassification::Unknown, FieldClassification::DeclaredNonSensitive,
        FieldClassification::Sensitive
    };
    for (bool settingsValid : booleans)
    for (bool enabled : booleans)
    for (FieldClassification classification : classifications)
    for (bool hostAllowed : booleans)
    for (bool focused : booleans)
    for (bool collapsed : booleans)
    for (bool composing : booleans)
    for (bool available : booleans)
    for (bool optedIn : booleans)
    {
        SafetyInputs input{ settingsValid, enabled, classification, hostAllowed,
            focused, collapsed, composing, available, optedIn };
        REQUIRE(DecideContextRead(input) == Expected(input));
        REQUIRE(IsAllowed(DecideContextRead(input)) ==
            (Expected(input) == SafetyDecision::AllowedByHostPolicy ||
             Expected(input) == SafetyDecision::AllowedByDeclaredScope));
    }

    const SafetyInputs eligible{ true, true, FieldClassification::Unknown,
        true, true, true, false, true, true };
    REQUIRE(DecideContextRead({ true, true, FieldClassification::Sensitive,
        true, true, true, false, true, true }) == SafetyDecision::DeniedSensitive);
    REQUIRE(DecideContextRead(eligible) == SafetyDecision::AllowedByHostPolicy);
    REQUIRE(DecideContextRead({ true, true, FieldClassification::Unknown,
        false, true, true, false, true, true }) == SafetyDecision::DeniedHostNotAllowlisted);
    REQUIRE(DecideContextRead({ false, true, FieldClassification::Unknown,
        true, true, true, false, true, true }) == SafetyDecision::DeniedInvalidSettings);

    for (FieldClassification classification : classifications)
    for (bool hostAllowed : booleans)
    for (bool focused : booleans)
    for (bool collapsed : booleans)
    for (bool composing : booleans)
    for (bool available : booleans)
    for (bool optedIn : booleans)
    {
        REQUIRE(DecideContextRead({ true, false, classification, hostAllowed,
            focused, collapsed, composing, available, optedIn }) == SafetyDecision::DeniedPaused);
    }

    const std::vector<std::wstring> allowlist{ L"notepad.exe" };
    REQUIRE(IsHostAllowlisted(allowlist, L"notepad.exe"));
    REQUIRE(IsHostAllowlisted(allowlist, L"NOTEPAD.EXE"));
    REQUIRE(!IsHostAllowlisted(allowlist, L"notepad.exe.evil"));
    REQUIRE(!IsHostAllowlisted(allowlist, L"xnotepad.exe"));
    REQUIRE(!IsHostAllowlisted(allowlist, L"C:\\Windows\\notepad.exe"));
    REQUIRE(!IsHostAllowlisted(allowlist, L""));
    REQUIRE(!IsHostAllowlisted({ L"*.exe" }, L"notepad.exe"));
    REQUIRE(!IsHostAllowlisted({ L"notepad?.exe" }, L"notepad.exe"));
    REQUIRE(!IsHostAllowlisted({ L"notepad.exe " }, L"notepad.exe"));
    REQUIRE(!IsHostAllowlisted({ L"\u00A0notepad.exe" }, L"notepad.exe"));
    return 0;
}
