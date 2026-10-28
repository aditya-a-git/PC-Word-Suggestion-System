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
#include <cwchar>

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
static constexpr wchar_t kSettingsPath[] = L"Software\\PCWord";
static void Trace(const wchar_t* message);

struct ExternalSettings
{
    bool valid = false;
    bool enabled = false;
    std::vector<std::wstring> allowlist;
};

static bool ReadExternalSettings(ExternalSettings* settings)
{
    if (!settings) return false;
    *settings = {};
    HKEY key = nullptr;
    LONG status = RegOpenKeyExW(HKEY_CURRENT_USER, kSettingsPath, 0, KEY_QUERY_VALUE, &key);
    if (status != ERROR_SUCCESS) return false;

    DWORD enabled = 0, type = 0, size = sizeof(enabled);
    status = RegQueryValueExW(key, L"Enabled", nullptr, &type,
        reinterpret_cast<BYTE*>(&enabled), &size);
    if (status != ERROR_SUCCESS || type != REG_DWORD || size != sizeof(enabled) || enabled > 1)
    {
        RegCloseKey(key);
        return false;
    }

    type = 0;
    size = 0;
    status = RegQueryValueExW(key, L"Allowlist", nullptr, &type, nullptr, &size);
    if (status != ERROR_SUCCESS || type != REG_MULTI_SZ ||
        size < 2 * sizeof(wchar_t) || size > 8192 || size % sizeof(wchar_t) != 0)
    {
        RegCloseKey(key);
        return false;
    }
    std::vector<wchar_t> raw(size / sizeof(wchar_t));
    status = RegQueryValueExW(key, L"Allowlist", nullptr, &type,
        reinterpret_cast<BYTE*>(raw.data()), &size);
    RegCloseKey(key);
    if (status != ERROR_SUCCESS || type != REG_MULTI_SZ ||
        size < 2 * sizeof(wchar_t) || raw[raw.size() - 1] != L'\0' ||
        raw[raw.size() - 2] != L'\0')
        return false;

    const size_t count = size / sizeof(wchar_t);
    if (count == 2 && raw[0] == L'\0' && raw[1] == L'\0')
    {
        settings->valid = true;
        settings->enabled = enabled == 1;
        return true;
    }
    size_t start = 0;
    while (start < count - 1 && raw[start] != L'\0')
    {
        size_t end = start;
        while (end < count && raw[end] != L'\0') ++end;
        if (end == count) return false;
        std::wstring entry(raw.data() + start, end - start);
        if (!pcword::input::IsValidExecutableName(entry)) return false;
        settings->allowlist.push_back(std::move(entry));
        start = end + 1;
    }
    if (start != count - 1 || raw[start] != L'\0') return false;

    settings->valid = true;
    settings->enabled = enabled == 1;
    return true;
}

static std::wstring GetHostExecutableName()
{
    std::vector<wchar_t> path(512);
    for (;;)
    {
        DWORD length = GetModuleFileNameW(nullptr, path.data(), static_cast<DWORD>(path.size()));
        if (length == 0) return {};
        if (length < path.size() - 1)
        {
            std::wstring fullPath(path.data(), length);
            size_t separator = fullPath.find_last_of(L"\\/");
            return separator == std::wstring::npos ? fullPath : fullPath.substr(separator + 1);
        }
        if (path.size() >= 32768) return {};
        path.resize(std::min<size_t>(path.size() * 2, 32768));
    }
}

static const wchar_t* DecisionName(pcword::input::SafetyDecision decision)
{
    using pcword::input::SafetyDecision;
    switch (decision)
    {
    case SafetyDecision::DeniedInvalidSettings: return L"invalid-settings";
    case SafetyDecision::DeniedPaused: return L"paused";
    case SafetyDecision::DeniedSensitive: return L"sensitive";
    case SafetyDecision::DeniedStructuralState: return L"structural-state";
    case SafetyDecision::DeniedHostNotAllowlisted: return L"host-not-allowlisted";
    case SafetyDecision::DeniedDiagnosticOptIn: return L"diagnostic-opt-in-required";
    case SafetyDecision::AllowedByHostPolicy: return L"allowed-by-host-policy";
    case SafetyDecision::AllowedByDeclaredScope: return L"allowed-by-declared-scope";
    default: return L"invalid-decision";
    }
}

static const wchar_t* ClassificationName(pcword::input::FieldClassification classification)
{
    using pcword::input::FieldClassification;
    switch (classification)
    {
    case FieldClassification::DeclaredNonSensitive: return L"safe";
    case FieldClassification::Sensitive: return L"sensitive";
    default: return L"unknown";
    }
}

static void TraceEditEvent(bool selectionKnown, bool collapsedSelection,
    bool caretRectAvailable, LONG beforeCount, LONG afterCount,
    const wchar_t* inputScopeStage, HRESULT inputScopeHr,
    pcword::input::FieldClassification classification,
    pcword::input::SafetyDecision decision)
{
    wchar_t message[512]{};
    swprintf_s(message,
        L"edit-event selection-collapsed=%s caret-rect=%s before-count=%ld after-count=%ld inputscope-stage=%s inputscope-hr=0x%08X classification=%s decision=%s allowed=%s",
        selectionKnown ? (collapsedSelection ? L"yes" : L"no") : L"unknown",
        caretRectAvailable ? L"yes" : L"no", beforeCount, afterCount,
        inputScopeStage ? inputScopeStage : L"not-read",
        static_cast<unsigned int>(inputScopeHr), ClassificationName(classification),
        DecisionName(decision), pcword::input::IsAllowed(decision) ? L"yes" : L"no");
    Trace(message);
}

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

static HRESULT UnregisterTsfProfile();

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
            L"PCWord TSF Feasibility Probe", static_cast<UINT>(-1), nullptr, 0, 0);
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

static HRESULT UnregisterTsfProfile()
{
    HRESULT result = S_OK;
    ITfInputProcessorProfiles* profiles = nullptr;
    HRESULT hr = CoCreateInstance(CLSID_TF_InputProcessorProfiles, nullptr,
        CLSCTX_INPROC_SERVER, IID_ITfInputProcessorProfiles,
        reinterpret_cast<void**>(&profiles));
    if (FAILED(hr))
        return hr;

    hr = profiles->Unregister(CLSID_PCWordTsfProbe);
    TraceHr(L"ITfInputProcessorProfiles::Unregister", hr);
    if (FAILED(hr)) result = hr;
    profiles->Release();

    ITfCategoryMgr* categories = nullptr;
    hr = CoCreateInstance(CLSID_TF_CategoryMgr, nullptr, CLSCTX_INPROC_SERVER,
        IID_ITfCategoryMgr, reinterpret_cast<void**>(&categories));
    if (FAILED(hr))
        return FAILED(result) ? result : hr;

    hr = categories->UnregisterCategory(CLSID_PCWordTsfProbe,
        GUID_TFCAT_TIP_KEYBOARD, CLSID_PCWordTsfProbe);
    TraceHr(L"ITfCategoryMgr::UnregisterCategory", hr);
    if (FAILED(hr) && SUCCEEDED(result)) result = hr;
    categories->Release();
    return result;
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
        if (m_threadMgr) return E_UNEXPECTED;

        ITfSource* source = nullptr;
        HRESULT hr = threadMgr->QueryInterface(IID_ITfSource,
            reinterpret_cast<void**>(&source));
        if (FAILED(hr))
        {
            TraceHr(L"ITfThreadMgr::QueryInterface(ITfSource)", hr);
            return hr;
        }

        DWORD sinkCookie = TF_INVALID_COOKIE;
        hr = source->AdviseSink(IID_ITfThreadMgrEventSink,
            static_cast<ITfThreadMgrEventSink*>(this), &sinkCookie);
        source->Release();
        if (FAILED(hr))
        {
            TraceHr(L"ITfSource::AdviseSink(ITfThreadMgrEventSink)", hr);
            return hr;
        }

        threadMgr->AddRef();
        m_threadMgr = threadMgr;
        m_clientId = clientId;
        m_threadSinkCookie = sinkCookie;
        Trace(L"Text service activated; observing TSF focus/edit notifications only.");
        return S_OK;
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
        ExternalSettings settings;
        const bool settingsValid = ReadExternalSettings(&settings);
        if (!settingsValid || !settings.enabled)
        {
            const auto decision = pcword::input::DecideContextRead({
                settingsValid, settings.enabled, pcword::input::FieldClassification::Unknown,
                false, false, false, false, false, false });
            TraceEditEvent(false, false, false, -1, -1, L"not-read", E_FAIL,
                pcword::input::FieldClassification::Unknown, decision);
            return S_OK;
        }

        // A collapsed range and a read-only edit cookie are used only inside TSF's
        // permitted callback. No text is copied to logs, files, or external processes.
        TF_SELECTION selection{};
        ULONG fetched = 0;
        HRESULT hr = context->GetSelection(cookie, TF_DEFAULT_SELECTION, 1,
            &selection, &fetched);
        if (hr != S_OK || fetched != 1 || !selection.range)
        {
            if (selection.range) selection.range->Release();
            const auto decision = pcword::input::SafetyDecision::DeniedStructuralState;
            TraceEditEvent(false, false, false, -1, -1, L"not-read", E_FAIL,
                pcword::input::FieldClassification::Unknown, decision);
            return S_OK;
        }

        BOOL isEmpty = FALSE;
        hr = selection.range->IsEmpty(cookie, &isEmpty);
        const wchar_t* inputScopeStage = L"not-read";
        HRESULT inputScopeHr = E_FAIL;
        const auto inputScopeStatus = GetInputScopeStatus(context, cookie, selection.range,
            &inputScopeStage, &inputScopeHr);
        LONG beforeCount = -1, afterCount = -1;
        RECT caretRect{};
        bool caretRectAvailable = false;
        bool compositionStateKnown = false;
        bool composing = IsComposing(context, &compositionStateKnown) ||
            selection.style.fInterimChar != FALSE;
        bool structuralStateAvailable = context == m_context && hr == S_OK &&
            compositionStateKnown && selection.range;
        const bool collapsedSelection = hr == S_OK && isEmpty != FALSE;

        ITfContextView* view = nullptr;
        HRESULT viewHr = context->GetActiveView(&view);
        if (viewHr == S_OK && view)
        {
            BOOL clipped = FALSE;
            viewHr = view->GetTextExt(cookie, selection.range, &caretRect, &clipped);
            caretRectAvailable = viewHr == S_OK && clipped == FALSE;
            view->Release();
        }
        else if (view) view->Release();

        const bool hostAllowed = pcword::input::IsHostAllowlisted(
            settings.allowlist, GetHostExecutableName());
        const auto decision = pcword::input::DecideContextRead({
            settingsValid, settings.enabled, inputScopeStatus, hostAllowed,
            context == m_context, collapsedSelection, composing,
            structuralStateAvailable && caretRectAvailable, IsContextReadOptedIn() });

        const bool allowRead = pcword::input::IsAllowed(decision);
        if (allowRead)
            ReadBoundedLengths(cookie, selection.range, &beforeCount, &afterCount);
        TraceEditEvent(hr == S_OK, collapsedSelection, caretRectAvailable,
            beforeCount, afterCount, inputScopeStage, inputScopeHr, inputScopeStatus, decision);
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
        HRESULT hr = context->QueryInterface(IID_ITfSource,
            reinterpret_cast<void**>(&source));
        if (hr != S_OK || !source)
        {
            TraceHr(L"ITfContext::QueryInterface(ITfSource)", hr);
            if (source) source->Release();
            return;
        }
        hr = source->AdviseSink(IID_ITfTextEditSink,
            static_cast<ITfTextEditSink*>(this), &m_contextSinkCookie);
        source->Release();
        if (hr == S_OK)
        {
            m_context = context;
            m_context->AddRef();
            Trace(L"Attached read-only edit notification sink to focused TSF context.");
        }
        else
        {
            TraceHr(L"ITfSource::AdviseSink(ITfTextEditSink)", hr);
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
            {
                HRESULT hr = source->UnadviseSink(m_contextSinkCookie);
                if (FAILED(hr)) TraceHr(L"ITfSource::UnadviseSink(ITfTextEditSink)", hr);
            }
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

    static bool IsComposing(ITfContext* context, bool* stateKnown)
    {
        if (stateKnown) *stateKnown = false;
        ITfContextComposition* composition = nullptr;
        HRESULT hr = context->QueryInterface(IID_ITfContextComposition,
            reinterpret_cast<void**>(&composition));
        if (hr != S_OK || !composition)
        {
            if (composition) composition->Release();
            return false;
        }
        IEnumITfCompositionView* enumerator = nullptr;
        hr = composition->EnumCompositions(&enumerator);
        composition->Release();
        if (hr != S_OK || !enumerator)
        {
            if (enumerator) enumerator->Release();
            return false;
        }
        ITfCompositionView* view = nullptr;
        ULONG fetched = 0;
        hr = enumerator->Next(1, &view, &fetched);
        enumerator->Release();
        if (view) view->Release();
        if (hr != S_OK && hr != S_FALSE)
        {
            return false;
        }
        if (stateKnown) *stateKnown = true;
        return fetched != 0;
    }

    // InputScope is a host hint. It is used only after explicit opt-in and never
    // read as proof that user-provided content is inherently non-sensitive.
    static pcword::input::FieldClassification GetInputScopeStatus(
        ITfContext* context, TfEditCookie cookie, ITfRange* range,
        const wchar_t** stage, HRESULT* status)
    {
        *stage = L"get-app-property";
        ITfReadOnlyProperty* property = nullptr;
        HRESULT hr = context->GetAppProperty(PCWORD_GUID_PROP_INPUTSCOPE, &property);
        *status = hr;
        if (hr != S_OK || !property)
        {
            if (property) property->Release();
            return pcword::input::FieldClassification::Unknown;
        }

        *stage = L"get-value";
        VARIANT value;
        VariantInit(&value);
        hr = property->GetValue(cookie, range, &value);
        property->Release();
        *status = hr;
        if (hr != S_OK)
        {
            VariantClear(&value);
            return pcword::input::FieldClassification::Unknown;
        }

        pcword::input::FieldClassification classification =
            pcword::input::FieldClassification::Unknown;
        if (value.vt == VT_UNKNOWN && value.punkVal)
        {
            *stage = L"query-interface";
            ITfInputScope* inputScope = nullptr;
            hr = value.punkVal->QueryInterface(IID_ITfInputScope,
                reinterpret_cast<void**>(&inputScope));
            *status = hr;
            if (hr == S_OK && inputScope)
            {
                *stage = L"get-input-scopes";
                InputScope* scopes = nullptr;
                UINT count = 0;
                hr = inputScope->GetInputScopes(&scopes, &count);
                *status = hr;
                if (hr == S_OK)
                    classification = pcword::input::ClassifyScopes(scopes, count);
                if (scopes) CoTaskMemFree(scopes);
                inputScope->Release();
            }
        }

        *stage = L"complete";
        hr = VariantClear(&value);
        if (FAILED(hr))
        {
            *status = hr;
            classification = pcword::input::FieldClassification::Unknown;
        }
        return classification;
    }
    static void ReadBoundedLengths(TfEditCookie cookie, ITfRange* caret,
        LONG* beforeCount, LONG* afterCount)
    {
        *beforeCount = -1;
        *afterCount = -1;

        ITfRange* before = nullptr;
        HRESULT hr = caret->Clone(&before);
        if (hr == S_OK && before)
        {
            hr = before->Collapse(cookie, TF_ANCHOR_START);
            if (hr == S_OK)
            {
                LONG shifted = 0;
                hr = before->ShiftStart(cookie, -kBeforeLimit, &shifted, nullptr);
                if (hr == S_OK)
                {
                    std::vector<WCHAR> buffer(static_cast<size_t>(kBeforeLimit) + 1, L'\0');
                    ULONG actual = 0;
                    hr = before->GetText(cookie, 0, buffer.data(),
                        static_cast<ULONG>(buffer.size()), &actual);
                    if (hr == S_OK)
                        *beforeCount = static_cast<LONG>(actual);
                    else
                        TraceHr(L"ITfRange::GetText(before caret)", hr);
                }
                else TraceHr(L"ITfRange::ShiftStart(before caret)", hr);
            }
            else TraceHr(L"ITfRange::Collapse(before caret)", hr);
            before->Release();
        }
        else TraceHr(L"ITfRange::Clone(before caret)", hr);

        ITfRange* after = nullptr;
        hr = caret->Clone(&after);
        if (hr == S_OK && after)
        {
            hr = after->Collapse(cookie, TF_ANCHOR_END);
            if (hr == S_OK)
            {
                LONG shifted = 0;
                hr = after->ShiftEnd(cookie, kAfterLimit, &shifted, nullptr);
                if (hr == S_OK)
                {
                    std::vector<WCHAR> buffer(static_cast<size_t>(kAfterLimit) + 1, L'\0');
                    ULONG actual = 0;
                    hr = after->GetText(cookie, 0, buffer.data(),
                        static_cast<ULONG>(buffer.size()), &actual);
                    if (hr == S_OK)
                        *afterCount = static_cast<LONG>(actual);
                    else
                        TraceHr(L"ITfRange::GetText(after caret)", hr);
                }
                else TraceHr(L"ITfRange::ShiftEnd(after caret)", hr);
            }
            else TraceHr(L"ITfRange::Collapse(after caret)", hr);
            after->Release();
        }
        else TraceHr(L"ITfRange::Clone(after caret)", hr);
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
        if (lock)
        {
            ++g_locks;
            return S_OK;
        }

        long current = g_locks.load();
        while (current > 0)
        {
            if (g_locks.compare_exchange_weak(current, current - 1))
                return S_OK;
        }
        return E_UNEXPECTED;
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

    const HRESULT profileHr = UnregisterTsfProfile();
    CoUninitialize();
    if (FAILED(profileHr))
        return profileHr;

    LONG result = RegDeleteTreeW(HKEY_CURRENT_USER,
        (L"Software\\Classes\\CLSID\\" + GuidString(CLSID_PCWordTsfProbe)).c_str());
    return result == ERROR_SUCCESS || result == ERROR_FILE_NOT_FOUND
        ? S_OK : HRESULT_FROM_WIN32(result);
}
