#include "../InputScopePolicy.h"

using namespace pcword::input;

static_assert(IsSensitiveScope(PasswordScope), "Password scope is sensitive.");
static_assert(IsSensitiveScope(NumericPasswordScope), "Numeric password scope is sensitive.");
static_assert(IsSensitiveScope(NumericPinScope), "Numeric PIN scope is sensitive.");
static_assert(IsSensitiveScope(AlphanumericPinScope), "Alphanumeric PIN scope is sensitive.");
static_assert(IsSensitiveScope(AlphanumericPinSetScope), "Alphanumeric PIN set scope is sensitive.");
static_assert(IsSensitiveScope(PrivateScope), "Private scope is sensitive.");
static_assert(!IsSensitiveScope(IS_TEXT), "Ordinary text scope is not explicitly sensitive.");

int main()
{
    return 0;
}
