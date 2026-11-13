#include "TsfEditSession.h"
#include <iostream>
#include <stdexcept>
#define REQUIRE(condition) do { if (!(condition)) { std::cerr << "failed at line " << __LINE__ << "\n"; return __LINE__; } } while (false)
// Controllable TSF request boundary. No real host compatibility is implied.
class Context final : public ITfContext
{
public:
    HRESULT requestResult = S_OK, sessionResult = S_OK;
    DWORD flags = 0;
    int requests = 0;
    STDMETHODIMP QueryInterface(REFIID, void**) override { return E_NOINTERFACE; }
    STDMETHODIMP_(ULONG) AddRef() override { return 1; }
    STDMETHODIMP_(ULONG) Release() override { return 1; }
    STDMETHODIMP RequestEditSession(TfClientId, ITfEditSession* session, DWORD value, HRESULT* result) override
    {
        ++requests; flags = value;
        *result = sessionResult;
        if (requestResult == S_OK && sessionResult == S_OK) *result = session->DoEditSession(42);
        return requestResult;
    }
    STDMETHODIMP InWriteSession(TfClientId, BOOL*) override { return E_NOTIMPL; }
    STDMETHODIMP GetSelection(TfEditCookie, ULONG, ULONG, TF_SELECTION*, ULONG*) override { return E_NOTIMPL; }
    STDMETHODIMP SetSelection(TfEditCookie, ULONG, const TF_SELECTION*) override { return E_NOTIMPL; }
    STDMETHODIMP GetStart(TfEditCookie, ITfRange**) override { return E_NOTIMPL; }
    STDMETHODIMP GetEnd(TfEditCookie, ITfRange**) override { return E_NOTIMPL; }
    STDMETHODIMP GetActiveView(ITfContextView**) override { return E_NOTIMPL; }
    STDMETHODIMP EnumViews(IEnumTfContextViews**) override { return E_NOTIMPL; }
    STDMETHODIMP GetStatus(TF_STATUS*) override { return E_NOTIMPL; }
    STDMETHODIMP GetProperty(REFGUID, ITfProperty**) override { return E_NOTIMPL; }
    STDMETHODIMP GetAppProperty(REFGUID, ITfReadOnlyProperty**) override { return E_NOTIMPL; }
    STDMETHODIMP TrackProperties(const GUID**, ULONG, const GUID**, ULONG, ITfReadOnlyProperty**) override { return E_NOTIMPL; }
    STDMETHODIMP EnumProperties(IEnumTfProperties**) override { return E_NOTIMPL; }
    STDMETHODIMP GetDocumentMgr(ITfDocumentMgr**) override { return E_NOTIMPL; }
    STDMETHODIMP CreateRangeBackup(TfEditCookie, ITfRange*, ITfRangeBackup**) override { return E_NOTIMPL; }
};

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
    Context context;
    for (int failure = 0; failure < 5; ++failure)
    {
        const int previousCalls = calls;
        context.requestResult = failure == 1 ? TF_E_LOCKED : failure == 2 ? TF_E_DISCONNECTED : S_OK;
        context.sessionResult = failure == 3 ? TF_E_SYNCHRONOUS : failure == 4 ? E_ACCESSDENIED : S_OK;
        auto result = pcword::edit::RequestSynchronousSession(&context, 1, true, objects,
            [&](TfEditCookie cookie) { ++calls; return cookie == 42 ? S_OK : E_FAIL; });
        REQUIRE(result.request == context.requestResult);
        REQUIRE(result.session == context.sessionResult);
        REQUIRE(calls == previousCalls + (failure == 0 ? 1 : 0));
        REQUIRE(context.flags == (TF_ES_SYNC | TF_ES_READWRITE));
        REQUIRE(objects == 0);
    }
    REQUIRE(context.requests == 5); // no automatic retry on either HRESULT channel
    context.sessionResult = S_OK;
    auto read = pcword::edit::RequestSynchronousSession(&context, 1, false, objects,
        [](TfEditCookie) { return E_FAIL; });
    REQUIRE(read.request == S_OK && read.session == E_FAIL);
    REQUIRE(context.flags == (TF_ES_SYNC | TF_ES_READ));
    REQUIRE(objects == 0);
    return 0;
}
