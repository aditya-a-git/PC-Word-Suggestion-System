#pragma once

#include "InputScopePolicy.h"

namespace pcword::input
{
    // Diagnostic reads require both an explicit user opt-in and a positive metadata
    // classification. Opt-in never overrides sensitive, missing, or ambiguous scope.
    constexpr bool CanReadContext(bool optedIn, FieldClassification classification) noexcept
    {
        return optedIn && classification == FieldClassification::DeclaredNonSensitive;
    }
}
