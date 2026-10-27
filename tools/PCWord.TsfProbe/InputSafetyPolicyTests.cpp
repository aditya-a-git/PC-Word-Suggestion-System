#include "InputSafetyPolicy.h"
#include <iostream>

#define REQUIRE(condition) do { if (!(condition)) { std::cerr << "failed at line " << __LINE__ << "\n"; return __LINE__; } } while (false)

using pcword::input::CanReadContext;
using pcword::input::FieldClassification;

int main()
{
    REQUIRE(!CanReadContext(false, FieldClassification::DeclaredNonSensitive));
    REQUIRE(CanReadContext(true, FieldClassification::DeclaredNonSensitive));
    REQUIRE(!CanReadContext(true, FieldClassification::Sensitive));
    REQUIRE(!CanReadContext(false, FieldClassification::Sensitive));
    REQUIRE(!CanReadContext(true, FieldClassification::Unknown));
    REQUIRE(!CanReadContext(false, FieldClassification::Unknown));
    return 0;
}
