#pragma once

#include <inputscope.h>
#include <cstddef>

namespace pcword::input
{
    enum class FieldClassification
    {
        Unknown,
        DeclaredNonSensitive,
        Sensitive
    };

    // InputScope is advisory metadata, not proof that the user's text is private-free.
    // Only ordinary, concrete scope values can be classified as declared non-sensitive;
    // IS_DEFAULT, sentinels, empty arrays, and out-of-range values remain unknown.
    inline bool IsSensitiveScope(InputScope scope) noexcept
    {
        switch (scope)
        {
        case IS_PASSWORD:
        case IS_NUMERIC_PASSWORD:
        case IS_NUMERIC_PIN:
        case IS_ALPHANUMERIC_PIN:
        case IS_ALPHANUMERIC_PIN_SET:
        case IS_PRIVATE:
            return true;
        default:
            return false;
        }
    }

    inline bool IsConcreteScope(InputScope scope) noexcept
    {
        return scope >= IS_URL && scope <= IS_CHAT_WITHOUT_EMOJI;
    }

    inline FieldClassification ClassifyScopes(const InputScope* scopes, std::size_t count) noexcept
    {
        if (scopes == nullptr || count == 0)
            return FieldClassification::Unknown;

        bool foundConcreteScope = false;
        for (std::size_t i = 0; i < count; ++i)
        {
            if (IsSensitiveScope(scopes[i]))
                return FieldClassification::Sensitive;
            if (!IsConcreteScope(scopes[i]))
                return FieldClassification::Unknown;
            foundConcreteScope = true;
        }

        return foundConcreteScope
            ? FieldClassification::DeclaredNonSensitive
            : FieldClassification::Unknown;
    }
}
