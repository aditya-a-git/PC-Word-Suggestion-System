#pragma once

#include <inputscope.h>
#include <cstddef>

namespace pcword::input
{
    // Keep these TSF enum values local instead of naming newer SDK enumerators.
    // The Windows SDK bundled on hosted runners may predate the PIN scope names,
    // while the documented InputScope numeric values remain stable.
    inline constexpr InputScope PasswordScope = static_cast<InputScope>(31);
    inline constexpr InputScope PrivateScope = static_cast<InputScope>(61);
    inline constexpr InputScope NumericPasswordScope = static_cast<InputScope>(63);
    inline constexpr InputScope NumericPinScope = static_cast<InputScope>(64);
    inline constexpr InputScope AlphanumericPinScope = static_cast<InputScope>(65);
    inline constexpr InputScope AlphanumericPinSetScope = static_cast<InputScope>(66);

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
        return scope == PasswordScope
            || scope == PrivateScope
            || scope == NumericPasswordScope
            || scope == NumericPinScope
            || scope == AlphanumericPinScope
            || scope == AlphanumericPinSetScope;
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
