#include "../InputScopePolicy.h"

using pcword::tsf::IsSensitiveInputScope;

static_assert(IsSensitiveInputScope(IS_PASSWORD), "Password scope is sensitive.");
static_assert(IsSensitiveInputScope(IS_NUMERICPIN), "Numeric PIN scope is sensitive.");
static_assert(IsSensitiveInputScope(IS_ALPHANUMERICPIN), "Alphanumeric PIN scope is sensitive.");
static_assert(IsSensitiveInputScope(IS_NUMERICPASSWORD), "Numeric password scope is sensitive.");
static_assert(IsSensitiveInputScope(IS_ALPHANUMERICPASSWORD), "Alphanumeric password scope is sensitive.");
static_assert(IsSensitiveInputScope(IS_PRIVATE), "Private scope is sensitive.");

static_assert(!IsSensitiveInputScope(IS_DEFAULT), "Default scope is not explicitly sensitive.");

int main()
{
    return 0;
}
