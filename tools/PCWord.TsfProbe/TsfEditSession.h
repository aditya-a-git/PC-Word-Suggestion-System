#pragma once

#include <windows.h>
#include <msctf.h>
#include <atomic>
#include <functional>
#include <new>
#include <utility>

namespace pcword::edit
{
    // Lifetime includes outstanding TSF references so the DLL cannot unload while
    // an edit-session COM object is alive. No exception crosses the COM boundary.
    class EditSession final : public ITfEditSession
    {
    public:
        EditSession(std::atomic<long>& objects, std::function<HRESULT(TfEditCookie)> callback)
            : m_objects(objects), m_callback(std::move(callback)) { ++m_objects; }
        ~EditSession() { --m_objects; }
        STDMETHODIMP QueryInterface(REFIID iid, void** value) override
        {
            if (!value) return E_POINTER;
            *value = nullptr;
            if (iid != IID_IUnknown && iid != IID_ITfEditSession) return E_NOINTERFACE;
            *value = static_cast<ITfEditSession*>(this);
            AddRef();
            return S_OK;
        }
        STDMETHODIMP_(ULONG) AddRef() override { return ++m_refs; }
        STDMETHODIMP_(ULONG) Release() override
        {
            ULONG remaining = --m_refs;
            if (!remaining) delete this;
            return remaining;
        }
        STDMETHODIMP DoEditSession(TfEditCookie cookie) override
        {
            if (m_called.exchange(true)) return E_UNEXPECTED;
            try { return m_callback(cookie); }
            catch (const std::bad_alloc&) { return E_OUTOFMEMORY; }
            catch (...) { return E_FAIL; }
        }
    private:
        std::atomic<ULONG> m_refs{ 1 };
        std::atomic<bool> m_called{ false };
        std::atomic<long>& m_objects;
        std::function<HRESULT(TfEditCookie)> m_callback;
    };

    struct SessionResult
    {
        HRESULT request = E_FAIL;
        HRESULT session = E_FAIL;
    };

    inline SessionResult RequestSynchronousSession(ITfContext* context, TfClientId client,
        bool write, std::atomic<long>& objects, std::function<HRESULT(TfEditCookie)> callback)
    {
        SessionResult result;
        if (!context || client == TF_CLIENTID_NULL) { result.request = E_INVALIDARG; return result; }
        auto* operation = new EditSession(objects, std::move(callback));
        result.request = context->RequestEditSession(client, operation,
            TF_ES_SYNC | (write ? TF_ES_READWRITE : TF_ES_READ), &result.session);
        operation->Release();
        return result;
    }
}
