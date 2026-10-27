#include <windows.h>
#include <msctf.h>
#include <inputscope.h>
#include <oleauto.h>
#include "InputSafetyPolicy.h"
#include <atomic>
#include <algorithm>
#include <iterator>
#include <new>
#include <string>
#include <vector>

#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "advapi32.lib")

// Stable identifiers for this experimental, per-user TSF text service.
static const CLSID CLSID_PCWordTsfProbe =
{ 0x6f7c1d8a, 0x9a4d, 0x4b2a, { 0x9e, 0x0f, 0x1c, 0x8a, 0x7f, 0x4b, 0x2d, 0x61 } };
static const GUID GUID_PCWordTsfProfile =
{ 0x0c3a11f4, 0x5f3b, 0x47a2, { 0x9e, 0x8d, 0x5a, 0x6f, 0x2d, 0x1c, 0x8b, 0x90 } };

// GUID_PROP_INPUTSCOPE is declared by inputscope.h but not exported by a
// link library in the Windows SDK used by CI. Keep the documented GUID value
// local to this translation unit to avoid an unresolved external.
static const GUID PCWORD_GUID_PROP_INPUTSCOPE =
{ 0x1713dd5a, 0x68e7, 0x4a5b, { 0x9a, 0xf6, 0x59, 0x2a, 0x59, 0x5c, 0x77, 0x8d } };

static HMODULE g_module = nullptr;
static std::atomic<long> g_objects{ 0 };
static std::atomic<long> g_locks{ 0 };
static constexpr LONG kBeforeLimit = 128;
static constexpr LONG kAfterLimit = 32;

static void Trace(const wchar_t* message)
{
    // Debug output contains status/metrics only. Never include text content.
    OutputDebugStringW(L"[PCWord.TSF] ");
    OutputDebugStringW(message);
    OutputDebugStringW(L"\r\n");
}

static void TraceHr(const wchar_t* operation, HRESULT hr)
{
    wchar_t message[160]{};
    swprintf_s(message, L"%s returned HRESULT 0x%08X",
        operation, static_cast<unsigned int>(hr));
    Trace(message);
}

static std::wstring GuidString(REFGUID guid)
{
    wchar_t value[64]{};
    StringFromGUID2(guid, value, static_cast<int>(std::size(value)));
    return value;
}

static HRESULT RegisterComServer()
{
    wchar_t modulePath[MAX_PATH]{};
    const DWORD pathLength = GetModuleFileNameW(g_module, modulePath, MAX_PATH);
    if (pathLength == 0)
        return HRESULT_FROM_WIN32(GetLastError());
    if (pathLength >= MAX_PATH)
        return HRESULT_FROM_WIN32(ERROR_INSUFFICIENT_BUFFER);

    const std::wstring clsidPath = L"Software\\Classes\\CLSID\\" + GuidString(CLSID_PCWordTsfProbe);
    const std::wstring serverPath = clsidPath + L"\\InprocServer32";
    HKEY key = nullptr;
    LONG result = RegCreateKeyExW(HKEY_CURRENT_USER, clsidPath.c_str(), 0, nullptr, 0,
        KEY_WRITE, nullptr, &key, nullptr);
    if (result != ERROR_SUCCESS)
        return HRESULT_FROM_WIN32(result);
    RegCloseKey(key);

    result = RegCreateKeyExW(HKEY_CURRENT_USER, serverPath.c_str(), 0, nullptr, 0,
        KEY_WRITE, nullptr, &key, nullptr);
    if (result != ERROR_SUCCESS)
    {
        RegDeleteTreeW(HKEY_CURRENT_USER, clsidPath.c_str());
        return HRESULT_FROM_WIN32(result);
    }

    result = RegSetValueExW(key, nullptr, 0, REG_SZ,
        reinterpret_cast<const BYTE*>(modulePath),
        static_cast<DWORD>((pathLength + 1) * sizeof(wchar_t)));
    if (result == ERROR_SUCCESS)
    {
        const wchar_t threadingModel[] = L"Apartment";
        result = RegSetValueExW(key, L"ThreadingModel", 0, REG_SZ,
            reinterpret_cast<const BYTE*>(threadingModel), sizeof(threadingModel));
    }
    RegCloseKey(key);

    if (result != ERROR_SUCCESS)
    {
        // Do not leave a partially registered COM class behind.
        RegDeleteTreeW(HKEY_CURRENT_USER, clsidPath.c_str());
        return HRESULT_FROM_WIN32(result);
    }
    return S_OK;
}

static void UnregisterTsfProfile();

static HRESULT RegisterTsfProfile()
{
    // Clean up this service's own state left by an interrupted prior attempt.
    UnregisterTsfProfile();

    ITfInputProcessorProfiles* profiles = nullptr;
    HRESULT hr = CoCreateInstance(CLSID_TF_InputProcessorProfiles, nullptr,
        CLSCTX_INPROC_SERVER, IID_ITfInputProcessorProfiles,
        reinterpret_cast<void**>(&profiles));
    if (FAILED(hr))
    {
        TraceHr(L"CoCreateInstance(ITfInputProcessorProfiles)", hr);
        return hr;
    }

    hr = profiles->Register(CLSID_PCWordTsfProbe);
    TraceHr(L"ITfInputProcessorProfiles::Register", hr);
    if (SUCCEEDED(hr))
    {
        // Register for the user's current Windows language (en-US / 0x0409)
        // rather than relying on the all-languages sentinel, which Windows may
        // register internally without exposing as a selectable keyboard profile.
        constexpr LANGID kProfileLanguage = 0x0409;
        hr = profiles->AddLanguageProfile(CLSID_PCWordTsfProbe,
            kProfileLanguage, GUID_PCWordTsfProfile,
            L"PCWord TSF Feasibility Probe", -1, nullptr, 0, 0);
        TraceHr(L"ITfInputProcessorProfiles::AddLanguageProfile", hr);
        if (SUCCEEDED(hr))
        {
            hr = profiles->EnableLanguageProfile(CLSID_PCWordTsfProbe,
                kProfileLanguage, GUID_PCWordTsfProfile, TRUE);
            TraceHr(L"ITfInputProcessorProfiles::EnableLanguageProfile", hr);
        }
        if (FAILED(hr))
        {
            profiles->Unregister(CLSID_PCWordTsfProbe);
            profiles->Release();
            return hr;
        }
    }
    profiles->Release();
    if (FAILED(hr))
        return hr;

    ITfCategoryMgr* categories = nullptr;
    hr = CoCreateInstance(CLSID_TF_CategoryMgr, nullptr, CLSCTX_INPROC_SERVER,
        IID_ITfCategoryMgr, reinterpret_cast<void**>(&categories));
    if (FAILED(hr))
    {
        TraceHr(L"CoCreateInstance(ITfCategoryMgr)", hr);
        ITfInputProcessorProfiles* rollbackProfiles = nullptr;
        if (SUCCEEDED(CoCreateInstance(CLSID_TF_InputProcessorProfiles, nullptr,
            CLSCTX_INPROC_SERVER, IID_ITfInputProcessorProfiles,
            reinterpret_cast<void**>(&rollbackProfiles))))
        {
            rollbackProfiles->Unregister(CLSID_PCWordTsfProbe);
            rollbackProfiles->Release();
        }
        return hr;
    }

    hr = categories->RegisterCategory(CLSID_PCWordTsfProbe,
        GUID_TFCAT_TIP_KEYBOARD, CLSID_PCWordTsfProbe);
    TraceHr(L"ITfCategoryMgr::RegisterCategory", hr);
    categories->Release();
    if (FAILED(hr))
    {
        ITfCategoryMgr* rollbackCategories = nullptr;
        if (SUCCEEDED(CoCreateInstance(CLSID_TF_CategoryMgr, nullptr,
            CLSCTX_INPROC_SERVER, IID_ITfCategoryMgr,
            reinterpret_cast<void**>(&rollbackCategories))))
        {
            rollbackCategories->UnregisterCategory(CLSID_PCWordTsfProbe,
                GUID_TFCAT_TIP_KEYBOARD, CLSID_PCWordTsfProbe);
            rollbackCategories->Release();
        }
        ITfInputProcessorProfiles* rollbackProfiles = nullptr;
        if (SUCCEEDED(CoCreateInstance(CLSID_TF_InputProcessorProfiles, nullptr,
            CLSCTX_INPROC_SERVER, IID_ITfInputProcessorProfiles,
            reinterpret_cast<void**>(&rollbackProfiles))))
        {
            rollbackProfiles->Unregister(CLSID_PCWordTsfProbe);
            rollbackProfiles->Release();
        }
    }
    return hr;
}

static void UnregisterTsfProfile()
{
    ITfInputProcessorProfiles* profiles = nullptr;
    if (SUCCEEDED(CoCreateInstance(CLSID_TF_InputProcessorProfiles, nullptr,
        CLSCTX_INPROC_SERVER, IID_ITfInputProcessorProfiles,
        reinterpret_cast<void**>(&profiles))))
    {
        profiles->Unregister(CLSID_PCWordTsfProbe);
        profiles->Release();
    }

    ITfCategoryMgr* categories = nullptr;
    if (SUCCEEDED(CoCreateInstance(CLSID_TF_CategoryMgr, nullptr, CLSCTX_INPROC_SERVER,
        IID_ITfCategoryMgr, reinterpret_cast<void**>(&categories))))
    {
        categories->UnregisterCategory(CLSID_PCWordTsfProbe,
            GUID_TFCAT_TIP_KEYBOARD, CLSID_PCWordTsfProbe);
        categories->Release();
    }
}

class TsfProbe final :
    public ITfTextInputProcessor,
    public ITfThreadMgrEventSink,
    public ITfTextEditSink
{
public:
    TsfProbe() { ++g_objects; }
    ~TsfProbe()
    {
        DetachContext();
        if (m_threadMgr && m_threadSinkCookie != TF_INVALID_COOKIE)
        {
            ITfSource* source = nullptr;
            if (SUCCEEDED(m_threadMgr->QueryInterface(IID_ITfSource,
                reinterpret_cast<void**>(&source))))
            {
                source->UnadviseSink(m_threadSinkCookie);
                source->Release();
            }
        }
        if (m_threadMgr) m_threadMgr->Release();
        --g_objects;
    }

    STDMETHODIMP QueryInterface(REFIID riid, void** object) override
    {
        if (!object) return E_POINTER;
        *object = nullptr;
        if (riid == IID_IUnknown || riid == IID_ITfTextInputProcessor)
            *object = static_cast<ITfTextInputProcessor*>(this);
        else if (riid == IID_ITfThreadMgrEventSink)
            *object = static_cast<ITfThreadMgrEventSink*>(this);
        else if (riid == IID_ITfTextEditSink)
            *object = static_cast<ITfTextEditSink*>(this);
        else
            return E_NOINTERFACE;
        AddRef();
        return S_OK;
    }

    STDMETHODIMP_(ULONG) AddRef() override { return ++m_refs; }
    STDMETHODIMP_(ULONG) Release() override
    {
        ULONG remaining = --m_refs;
        if (remaining == 0) delete this;
        return remaining;
    }

    STDMETHODIMP Activate(ITfThreadMgr* threadMgr, TfClientId clientId) override
    {
        if (!threadMgr) return E_INVALIDARG;
        m_threadMgr = threadMgr;
        m_threadMgr->AddRef();
        m_clientId = clientId;

        ITfSource* source = nullptr;
        HRESULT hr = m_threadMgr->QueryInterface(IID_ITfSource,
            reinterpret_cast<void**>(&source));
        if (SUCCEEDED(hr))
        {
            hr = source->AdviseSink(IID_ITfThreadMgrEventSink,
                static_cast<ITfThreadMgrEventSink*>(this), &m_threadSinkCookie);
            source->Release();
        }
        if (SUCCEEDED(hr))
            Trace(L"Text service activated; observing TSF focus/edit notifications only.");
        return hr;
    }

    STDMETHODIMP Deactivate() override
    {
        DetachContext();
        if (m_threadMgr && m_threadSinkCookie != TF_INVALID_COOKIE)
        {
            ITfSource* source = nullptr;
            if (SUCCEEDED(m_threadMgr->QueryInterface(IID_ITfSource,
                reinterpret_cast<void**>(&source))))
            {
                source->UnadviseSink(m_threadSinkCookie);
                source->Release();
            }
            m_threadSinkCookie = TF_INVALID_COOKIE;
        }
        if (m_threadMgr) { m_threadMgr->Release(); m_threadMgr = nullptr; }
        Trace(L"Text service deactivated.");
        return S_OK;
    }

    STDMETHODIMP OnInitDocumentMgr(ITfDocumentMgr*) override { return S_OK; }
    STDMETHODIMP OnUninitDocumentMgr(ITfDocumentMgr*) override { return S_OK; }

    STDMETHODIMP OnSetFocus(ITfDocumentMgr* focused, ITfDocumentMgr*) override
    {
        DetachContext();
        if (focused)
        {
            ITfContext* context = nullptr;
            if (SUCCEEDED(focused->GetTop(&context)) && context)
            {
                AttachContext(context);
                context->Release();
            }
        }
        Trace(L"TSF document focus changed.");
        return S_OK;
    }

    STDMETHODIMP OnPushContext(ITfContext* context) override
    {
        AttachContext(context);
        return S_OK;
    }

    STDMETHODIMP OnPopContext(ITfContext* context) override
    {
        if (context == m_context)
            DetachContext();
        return S_OK;
    }

    STDMETHODIMP OnEndEdit(ITfContext* context, TfEditCookie cookie,
        ITfEditRecord*) override
    {
        if (!context) return E_INVALIDARG;
        // A collapsed range and a read-only edit cookie are used only inside TSF's
        // permitted callback. No text is copied to logs, files, or external processes.
        TF_SELECTION selection{};
        ULONG fetched = 0;
        HRESULT hr = context->GetSelection(cookie, TF_DEFAULT_SELECTION, 1,
            &selection, &fetched);
        if (FAILED(hr) || fetched != 1 || !selection.range)
        {
            Trace(L"Edit notification received; selection/caret unavailable.");
            if (selection.range) selection.range->Release();
            return S_OK;
        }

        BOOL isEmpty = FALSE;
        hr = selection.range->IsEmpty(cookie, &isEmpty);
        if (FAILED(hr) || !isEmpty)
        {
            Trace(L"Edit notification received; non-collapsed or unknown selection, context skipped.");
            selection.range->Release();
            return S_OK;
        }

        const pcword::tsf::InputScopeStatus inputScopeStatus =
            GetInputScopeStatus(context, cookie, selection.range);
        const bool optedIn = IsContextReadOptedIn();
        const bool allowRead = pcword::tsf::CanReadContext(optedIn, inputScopeStatus);
        LONG beforeCount = -1, afterCount = -1;
        RECT caretRect{};
        bool caretRectAvailable = false;

        ITfContextView* view = nullptr;
        if (SUCCEEDED(context->GetActiveView(&view)) && view)
        {
            BOOL clipped = FALSE;
            caretRectAvailable = SUCCEEDED(view->GetTextExt(cookie, selection.range,
                &caretRect, &clipped));
            view->Release();
        }

        if (allowRead)
            ReadBoundedLengths(cookie, selection.range, &beforeCount, &afterCount);

        wchar_t summary[256]{};
        const wchar_t* scopeLabel =
            inputScopeStatus == pcword::tsf::InputScopeStatus::Sensitive ? L"password" :
            (inputScopeStatus == pcword::tsf::InputScopeStatus::NonSensitive
                ? L"non-password" : L"unknown");
        swprintf_s(summary, L"Edit event: input-scope=%s opt-in=%s selection=collapsed "
            L"context-read=%s before=%ld after=%ld caret-rect=%s rect=(%ld,%ld,%ld,%ld)",
            scopeLabel,
            optedIn ? L"yes" : L"no",
            allowRead ? L"attempted" : L"skipped",
            beforeCount, afterCount, caretRectAvailable ? L"yes" : L"no",
            caretRect.left, caretRect.top, caretRect.right, caretRect.bottom);
        Trace(summary);
        selection.range->Release();
        return S_OK;
    }

private:
    std::atomic<ULONG> m_refs{ 1 };
    ITfThreadMgr* m_threadMgr = nullptr;
    ITfContext* m_context = nullptr;
    TfClientId m_clientId = TF_CLIENTID_NULL;
    DWORD m_threadSinkCookie = TF_INVALID_COOKIE;
    DWORD m_contextSinkCookie = TF_INVALID_COOKIE;

    void AttachContext(ITfContext* context)
    {
        if (!context || context == m_context) return;
        DetachContext();

        ITfSource* source = nullptr;
        if (FAILED(context->QueryInterface(IID_ITfSource,
            reinterpret_cast<void**>(&source))))
        {
            Trace(L"TSF context does not expose an event source.");
            return;
        }
        HRESULT hr = source->AdviseSink(IID_ITfTextEditSink,
            static_cast<ITfTextEditSink*>(this), &m_contextSinkCookie);
        source->Release();
        if (SUCCEEDED(hr))
        {
            m_context = context;
            m_context->AddRef();
            Trace(L"Attached read-only edit notification sink to focused TSF context.");
        }
        else
        {
            Trace(L"TSF context edit notifications unavailable.");
        }
    }

    void DetachContext()
    {
        if (!m_context) return;
        ITfSource* source = nullptr;
        if (SUCCEEDED(m_context->QueryInterface(IID_ITfSource,
            reinterpret_cast<void**>(&source))))
        {
            if (m_contextSinkCookie != TF_INVALID_COOKIE)
                source->UnadviseSink(m_contextSinkCookie);
            source->Release();
        }
        m_contextSinkCookie = TF_INVALID_COOKIE;
        m_context->Release();
        m_context = nullptr;
    }

    static bool IsContextReadOptedIn()
    {
        wchar_t value[8]{};
        DWORD length = GetEnvironmentVariableW(L"PCWORD_TSF_ALLOW_CONTEXT_READ",
            value, static_cast<DWORD>(std::size(value)));
        return length == 1 && value[0] == L'1';
    }

    // Return a positive safe classification only when metadata is available and
    // reports at least one scope. All failures and ambiguous results are Unknown.
    static pcword::tsf::InputScopeStatus GetInputScopeStatus(
        ITfContext* context, TfEditCookie cookie, ITfRange* range)
    {
        // Microsoft documents GUID_PROP_INPUTSCOPE retrieval through
        // ITfContext::GetAppProperty followed by ITfReadOnlyProperty::GetValue.
        // A failed GetValue must remain Unknown; do not infer safety.
        ITfReadOnlyProperty* property = nullptr;
        HRESULT hr = context->GetAppProperty(PCWORD_GUID_PROP_INPUTSCOPE, &property);
        if (FAILED(hr) || !property)
        {
            TraceHr(L"ITfContext::GetAppProperty(GUID_PROP_INPUTSCOPE)", hr);
            if (property) property->Release();
            return pcword::tsf::InputScopeStatus::Unknown;
        }

        VARIANT value;
        VariantInit(&value);
        hr = property->GetValue(cookie, range, &value);
        property->Release();
        if (FAILED(hr))
        {
            TraceHr(L"GUID_PROP_INPUTSCOPE GetValue", hr);
            VariantClear(&value);
            return pcword::tsf::InputScopeStatus::Unknown;
        }

        wchar_t message[128]{};
        swprintf_s(message, L"InputScope VARIANT type=%u",
            static_cast<unsigned int>(value.vt));
        Trace(message);

        pcword::tsf::InputScopeStatus scopeStatus = pcword::tsf::InputScopeStatus::Unknown;
        if (value.vt == VT_UNKNOWN && value.punkVal)
        {
            ITfInputScope* inputScope = nullptr;
            hr = value.punkVal->QueryInterface(IID_ITfInputScope,
                reinterpret_cast<void**>(&inputScope));
            if (FAILED(hr) || !inputScope)
            {
                TraceHr(L"InputScope QueryInterface(ITfInputScope)", hr);
            }
            else
            {
                InputScope* scopes = nullptr;
                UINT count = 0;
                hr = inputScope->GetInputScopes(&scopes, &count);
                if (FAILED(hr))
                {
                    TraceHr(L"ITfInputScope::GetInputScopes", hr);
                }
                else
                {
                    swprintf_s(message, L"InputScope count=%u",
                        static_cast<unsigned int>(count));
                    Trace(message);

                    // Empty or unavailable metadata is not proof that a field
                    // is safe to read. Fail closed unless at least one scope
                    // was positively reported and none is IS_PASSWORD.
                    if (count > 0 && scopes)
                    {
                        scopeStatus = pcword::tsf::InputScopeStatus::NonSensitive;
                        for (UINT i = 0; i < count; ++i)
                        {
                            if (scopes[i] == IS_PASSWORD)
                            {
                                scopeStatus = pcword::tsf::InputScopeStatus::Sensitive;
                                break;
                            }
                        }
                    }
                }

                if (scopes) CoTaskMemFree(scopes);
                inputScope->Release();
            }
        }
        else
        {
            Trace(L"InputScope value is not a VT_UNKNOWN interface.");
        }

        VariantClear(&value);
        return scopeStatus;
    }

    static void ReadBoundedLengths(TfEditCookie cookie, ITfRange* caret,
        LONG* beforeCount, LONG* afterCount)
    {
        *beforeCount = -1;
        *afterCount = -1;

        ITfRange* before = nullptr;
        if (SUCCEEDED(caret->Clone(&before)) && before)
        {
            if (SUCCEEDED(before->Collapse(cookie, TF_ANCHOR_START)))
            {
                LONG shifted = 0;
                if (SUCCEEDED(before->ShiftStart(cookie, -kBeforeLimit, &shifted, nullptr)))
                {
                    std::vector<WCHAR> buffer(static_cast<size_t>(kBeforeLimit) + 1, L'\0');
                    ULONG actual = 0;
                    if (SUCCEEDED(before->GetText(cookie, 0, buffer.data(),
                        static_cast<ULONG>(buffer.size()), &actual)))
                        *beforeCount = static_cast<LONG>(actual);
                }
            }
            before->Release();
        }

        ITfRange* after = nullptr;
        if (SUCCEEDED(caret->Clone(&after)) && after)
        {
            if (SUCCEEDED(after->Collapse(cookie, TF_ANCHOR_END)))
            {
                LONG shifted = 0;
                if (SUCCEEDED(after->ShiftEnd(cookie, kAfterLimit, &shifted, nullptr)))
                {
                    std::vector<WCHAR> buffer(static_cast<size_t>(kAfterLimit) + 1, L'\0');
                    ULONG actual = 0;
                    if (SUCCEEDED(after->GetText(cookie, 0, buffer.data(),
                        static_cast<ULONG>(buffer.size()), &actual)))
                        *afterCount = static_cast<LONG>(actual);
                }
            }
            after->Release();
        }
    }
};

class TsfProbeClassFactory final : public IClassFactory
{
public:
    TsfProbeClassFactory() { ++g_objects; }
    ~TsfProbeClassFactory() { --g_objects; }

    STDMETHODIMP QueryInterface(REFIID riid, void** object) override
    {
        if (!object) return E_POINTER;
        *object = nullptr;
        if (riid != IID_IUnknown && riid != IID_IClassFactory) return E_NOINTERFACE;
        *object = static_cast<IClassFactory*>(this);
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
    STDMETHODIMP CreateInstance(IUnknown* outer, REFIID riid, void** object) override
    {
        if (outer) return CLASS_E_NOAGGREGATION;
        auto* service = new (std::nothrow) TsfProbe();
        if (!service) return E_OUTOFMEMORY;
        HRESULT hr = service->QueryInterface(riid, object);
        service->Release();
        return hr;
    }
    STDMETHODIMP LockServer(BOOL lock) override
    {
        if (lock) ++g_locks; else --g_locks;
        return S_OK;
    }

private:
    std::atomic<ULONG> m_refs{ 1 };
};

extern "C" BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_module = instance;
        DisableThreadLibraryCalls(instance);
    }
    return TRUE;
}

extern "C" HRESULT WINAPI DllCanUnloadNow(void)
{
    return g_objects.load() == 0 && g_locks.load() == 0 ? S_OK : S_FALSE;
}

extern "C" HRESULT WINAPI DllGetClassObject(
    REFCLSID clsid, REFIID riid, void** object)
{
    if (clsid != CLSID_PCWordTsfProbe) return CLASS_E_CLASSNOTAVAILABLE;
    auto* factory = new (std::nothrow) TsfProbeClassFactory();
    if (!factory) return E_OUTOFMEMORY;
    HRESULT hr = factory->QueryInterface(riid, object);
    factory->Release();
    return hr;
}

STDAPI DllRegisterServer(void)
{
    HRESULT hr = RegisterComServer();
    if (FAILED(hr)) return hr;

    const HRESULT initHr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    if (FAILED(initHr))
    {
        RegDeleteTreeW(HKEY_CURRENT_USER,
            (L"Software\\Classes\\CLSID\\" + GuidString(CLSID_PCWordTsfProbe)).c_str());
        return initHr;
    }

    hr = RegisterTsfProfile();
    CoUninitialize();
    if (FAILED(hr))
    {
        // RegisterTsfProfile rolls back partial TSF state; remove the COM entry too.
        RegDeleteTreeW(HKEY_CURRENT_USER,
            (L"Software\\Classes\\CLSID\\" + GuidString(CLSID_PCWordTsfProbe)).c_str());
    }
    return hr;
}

STDAPI DllUnregisterServer(void)
{
    const HRESULT initHr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    if (FAILED(initHr))
        return initHr;

    UnregisterTsfProfile();
    CoUninitialize();

    LONG result = RegDeleteTreeW(HKEY_CURRENT_USER,
        (L"Software\\Classes\\CLSID\\" + GuidString(CLSID_PCWordTsfProbe)).c_str());
    return result == ERROR_SUCCESS || result == ERROR_FILE_NOT_FOUND
        ? S_OK : HRESULT_FROM_WIN32(result);
}
