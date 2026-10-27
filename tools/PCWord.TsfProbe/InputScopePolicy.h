#pragma once

#include <inputscope.h>

// InputScope is a hint supplied by the host, not a universal sensitivity
// guarantee. Explicit private, password, and PIN scopes must never be read.
namespace pcword::tsf
{
    constexpr bool IsSensitiveInputScope(InputScope scope) noexcept
    {
        switch (scope)
        {
        case IS_PASSWORD:
        case IS_NUMERICPIN:
        case IS_ALPHANUMERICPIN:
        case IS_NUMERICPASSWORD:
        case IS_ALPHANUMERICPASSWORD:
        case IS_PRIVATE:
            return true;
        default:
            return false;
        }
    }
}
