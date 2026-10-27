#pragma once

// Input-safety decisions are intentionally independent of the TSF API lookup.
// The TSF adapter must classify a field as non-sensitive before any context
// read is permitted. Missing, failed, or ambiguous metadata is Unknown.
namespace pcword::tsf
{
    enum class InputScopeStatus
    {
        NonSensitive,
        Sensitive,
        Unknown
    };

    constexpr bool CanReadContext(bool optedIn, InputScopeStatus status) noexcept
    {
        return optedIn && status == InputScopeStatus::NonSensitive;
    }
}
