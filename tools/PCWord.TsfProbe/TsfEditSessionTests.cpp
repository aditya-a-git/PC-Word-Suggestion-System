#include "TsfEditSession.h"
#include <iostream>
#include <stdexcept>
#define REQUIRE(condition) do { if (!(condition)) { std::cerr << "failed at line " << __LINE__ << "\n"; return __LINE__; } } while (false)
int main()
{
    std::atomic<long> objects{ 0 };
    int calls = 0;
    auto* session = new pcword::edit::EditSession(objects, [&](TfEditCookie cookie) {
        ++calls;
        return cookie == 42 ? S_OK : E_FAIL;
    });
    REQUIRE(objects == 1);
    void* unknown = nullptr;
    REQUIRE(session->QueryInterface(IID_IUnknown, &unknown) == S_OK);
    static_cast<IUnknown*>(unknown)->Release();
    REQUIRE(session->QueryInterface(IID_ITfContext, &unknown) == E_NOINTERFACE && !unknown);
    REQUIRE(session->QueryInterface(IID_IUnknown, nullptr) == E_POINTER);
    REQUIRE(session->DoEditSession(42) == S_OK);
    REQUIRE(session->DoEditSession(42) == E_UNEXPECTED && calls == 1);
    session->Release();
    REQUIRE(objects == 0);
    for (int failure = 0; failure < 3; ++failure)
    {
        auto* failed = new pcword::edit::EditSession(objects, [failure](TfEditCookie) -> HRESULT {
            if (failure == 0) throw std::bad_alloc();
            if (failure == 1) throw std::runtime_error("provider");
            return TF_E_LOCKED;
        });
        REQUIRE(failed->DoEditSession(1) ==
            (failure == 0 ? E_OUTOFMEMORY : failure == 1 ? E_FAIL : TF_E_LOCKED));
        failed->Release();
    }
    REQUIRE(objects == 0);
    return 0;
}
