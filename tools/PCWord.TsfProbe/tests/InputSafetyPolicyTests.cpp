#include "../InputSafetyPolicy.h"

using pcword::tsf::CanReadContext;
using pcword::tsf::InputScopeStatus;

static_assert(!CanReadContext(false, InputScopeStatus::NonSensitive),
    "Context reads require explicit opt-in.");
static_assert(!CanReadContext(true, InputScopeStatus::Sensitive),
    "Sensitive fields must never permit context reads.");
static_assert(!CanReadContext(false, InputScopeStatus::Sensitive),
    "Opt-out must deny reads even for sensitive fields.");
static_assert(!CanReadContext(false, InputScopeStatus::Unknown),
    "Unknown input scope must deny reads.");
static_assert(!CanReadContext(true, InputScopeStatus::Unknown),
    "Opt-in must not override unknown input scope.");
static_assert(CanReadContext(true, InputScopeStatus::NonSensitive),
    "Only opted-in, positively non-sensitive fields may permit reads.");

int main()
{
    return 0;
}
