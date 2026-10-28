#include "../InputSafetyPolicy.h"

using namespace pcword::input;

constexpr SafetyInputs eligibleUnknown{ true, true, FieldClassification::Unknown,
    true, true, true, false, true, true };
static_assert(DecideContextRead(eligibleUnknown) == SafetyDecision::AllowedByHostPolicy,
    "Unknown scope may pass only the explicit host and diagnostic gates.");
static_assert(DecideContextRead({ true, true, FieldClassification::Sensitive,
    true, true, true, false, true, true }) == SafetyDecision::DeniedSensitive,
    "Sensitive classification must override host allowlisting and diagnostic opt-in.");
static_assert(DecideContextRead({ true, false, FieldClassification::Unknown,
    true, true, true, false, true, true }) == SafetyDecision::DeniedPaused,
    "Paused policy must deny context reads.");
static_assert(DecideContextRead({ false, true, FieldClassification::Unknown,
    true, true, true, false, true, true }) == SafetyDecision::DeniedInvalidSettings,
    "Missing or malformed settings must deny.");

int main()
{
    return 0;
}
