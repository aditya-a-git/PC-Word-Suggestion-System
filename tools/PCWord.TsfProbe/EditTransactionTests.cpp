#include "EditTransaction.h"
#include <iostream>
#include <stdexcept>

#define REQUIRE(condition) do { if (!(condition)) { std::cerr << "failed at line " << __LINE__ << "\n"; return __LINE__; } } while (false)

struct Backend
{
    bool valid = true, replace = true, verify = true;
    int validations = 0, writes = 0, reads = 0;
    bool Validate() { ++validations; return valid; }
    bool Replace() { ++writes; return replace; }
    bool Verify() { ++reads; return verify; }
};

int main()
{
    using namespace pcword::edit;
    Ticket ticket{ 2, 100, true };
    Backend ok;
    REQUIRE(Apply(ticket, 2, 200, ok) == Result::Verified);
    REQUIRE(ok.writes == 1 && ok.reads == 1);
    REQUIRE(Apply(ticket, 2, 201, ok) == Result::Rejected);
    REQUIRE(ok.writes == 1);
    for (int failure = 0; failure < 4; ++failure)
    {
        Ticket stale{ 2, 100, failure != 3 };
        Backend backend;
        auto result = Apply(stale, failure == 0 ? 3 : 2,
            failure == 1 ? 5101 : failure == 2 ? 99 : 200, backend);
        REQUIRE(result == Result::Rejected);
        REQUIRE(backend.validations == 0 && backend.writes == 0 && !stale.armed);
    }
    for (int failure = 0; failure < 3; ++failure)
    {
        Ticket request{ 2, 100, true };
        Backend backend;
        backend.valid = failure != 0;
        backend.replace = failure != 1;
        backend.verify = failure != 2;
        REQUIRE(Apply(request, 2, 200, backend) ==
            (failure == 0 ? Result::Rejected : Result::Uncertain));
        REQUIRE(backend.writes == (failure == 0 ? 0 : 1));
        REQUIRE(backend.reads == (failure == 2 ? 1 : 0));
        REQUIRE(Apply(request, 2, 201, backend) == Result::Rejected);
    }
    struct ThrowingBackend : Backend { bool fail = true; bool Replace() { ++writes; if (fail) throw std::runtime_error("provider failure"); return true; } } throwing;
    Ticket request{ 2, 100, true };
    try { Apply(request, 2, 200, throwing); REQUIRE(false); }
    catch (const std::runtime_error&) { REQUIRE(!request.armed && throwing.writes == 1); }
    return 0;
}
