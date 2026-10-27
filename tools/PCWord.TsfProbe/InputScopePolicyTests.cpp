#include "InputScopePolicy.h"
#include <array>
#include <iostream>

#define REQUIRE(condition) do { if (!(condition)) { std::cerr << "failed at line " << __LINE__ << "\n"; return __LINE__; } } while (false)

using pcword::input::ClassifyScopes;
using pcword::input::FieldClassification;
using pcword::input::PasswordScope;
using pcword::input::PrivateScope;
using pcword::input::NumericPasswordScope;
using pcword::input::NumericPinScope;
using pcword::input::AlphanumericPinScope;
using pcword::input::AlphanumericPinSetScope;

int main()
{
    REQUIRE(ClassifyScopes(nullptr, 0) == FieldClassification::Unknown);

    const InputScope normal[] = { IS_TEXT };
    REQUIRE(ClassifyScopes(normal, 1) == FieldClassification::DeclaredNonSensitive);
    const InputScope multipleNormal[] = { IS_TEXT, IS_CHAT };
    REQUIRE(ClassifyScopes(multipleNormal, 2) == FieldClassification::DeclaredNonSensitive);
    const InputScope defaultScope[] = { IS_DEFAULT };
    REQUIRE(ClassifyScopes(defaultScope, 1) == FieldClassification::Unknown);
    const InputScope unsupported[] = { static_cast<InputScope>(99) };
    REQUIRE(ClassifyScopes(unsupported, 1) == FieldClassification::Unknown);
    const InputScope sentinel[] = { IS_PHRASELIST };
    REQUIRE(ClassifyScopes(sentinel, 1) == FieldClassification::Unknown);
    const InputScope mixedUnknown[] = { IS_TEXT, static_cast<InputScope>(99) };
    REQUIRE(ClassifyScopes(mixedUnknown, 2) == FieldClassification::Unknown);

    constexpr std::array<InputScope, 6> sensitiveScopes = {
        PasswordScope, NumericPasswordScope, NumericPinScope,
        AlphanumericPinScope, AlphanumericPinSetScope, PrivateScope
    };
    for (InputScope scope : sensitiveScopes)
    {
        const InputScope values[] = { IS_TEXT, scope };
        REQUIRE(ClassifyScopes(values, 2) == FieldClassification::Sensitive);
    }
    return 0;
}
