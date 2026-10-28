#pragma once

#include "InputScopePolicy.h"
#include <cwctype>
#include <string>
#include <vector>

namespace pcword::input
{
    enum class SafetyDecision
    {
        DeniedInvalidSettings,
        DeniedPaused,
        DeniedSensitive,
        DeniedStructuralState,
        DeniedHostNotAllowlisted,
        DeniedDiagnosticOptIn,
        AllowedByHostPolicy,
        AllowedByDeclaredScope
    };

    struct SafetyInputs
    {
        bool settingsValid;
        bool enabled;
        FieldClassification classification;
        bool hostAllowlisted;
        bool focused;
        bool collapsedSelection;
        bool composing;
        bool contextAvailable;
        bool diagnosticOptIn;
    };

    // First matching denial wins. Diagnostic opt-in is an extra final gate.
    constexpr SafetyDecision DecideContextRead(const SafetyInputs& input) noexcept
    {
        if (!input.settingsValid) return SafetyDecision::DeniedInvalidSettings;
        if (!input.enabled) return SafetyDecision::DeniedPaused;
        if (input.classification == FieldClassification::Sensitive)
            return SafetyDecision::DeniedSensitive;
        if (!input.focused || !input.collapsedSelection || input.composing || !input.contextAvailable)
            return SafetyDecision::DeniedStructuralState;
        if (!input.hostAllowlisted) return SafetyDecision::DeniedHostNotAllowlisted;
        if (!input.diagnosticOptIn) return SafetyDecision::DeniedDiagnosticOptIn;
        return input.classification == FieldClassification::DeclaredNonSensitive
            ? SafetyDecision::AllowedByDeclaredScope
            : SafetyDecision::AllowedByHostPolicy;
    }

    constexpr bool IsAllowed(SafetyDecision decision) noexcept
    {
        return decision == SafetyDecision::AllowedByHostPolicy ||
            decision == SafetyDecision::AllowedByDeclaredScope;
    }

    inline bool IsValidExecutableName(const std::wstring& name) noexcept
    {
        if (name.empty()) return false;
        for (wchar_t character : name)
        {
            const unsigned int code = static_cast<unsigned int>(character);
            const bool whitespace = std::iswspace(character) != 0 ||
                (code >= 0x2000 && code <= 0x200A) || code == 0x0085 ||
                code == 0x00A0 || code == 0x1680 || code == 0x2028 ||
                code == 0x2029 || code == 0x202F || code == 0x205F || code == 0x3000;
            if (whitespace || character == L'\\' || character == L'/' ||
                character == L'*' || character == L'?')
                return false;
        }
        return true;
    }

    inline bool IsHostAllowlisted(const std::vector<std::wstring>& allowlist,
        const std::wstring& executableName) noexcept
    {
        if (!IsValidExecutableName(executableName)) return false;
        for (const auto& entry : allowlist)
        {
            if (!IsValidExecutableName(entry)) continue;
            if (_wcsicmp(entry.c_str(), executableName.c_str()) == 0)
                return true;
        }
        return false;
    }
}
