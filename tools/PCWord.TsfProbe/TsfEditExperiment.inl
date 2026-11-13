// Included only in the separate experimental TIP class body. The diagnostic
// PCWordTsfProbe build contains no keyboard interception or document writes.
    pcword::edit::Ticket m_ticket;
    Microsoft::WRL::ComPtr<ITfRange> m_capturedCaret;
    std::array<UCHAR, 32> m_fingerprint{};
    bool m_inSession = false;
    bool m_keySink = false, m_captureKey = false, m_applyKey = false;

    void ClearCapture()
    {
        m_ticket.armed = false;
        m_capturedCaret.Reset();
        SecureZeroMemory(m_fingerprint.data(), m_fingerprint.size());
    }

    static bool EditOptedIn()
    {
        wchar_t value[8]{};
        return GetEnvironmentVariableW(L"PCWORD_TSF_ALLOW_EDIT_EXPERIMENT", value,
            static_cast<DWORD>(std::size(value))) == 1 && value[0] == L'1';
    }

    HRESULT RegisterExperimentKeys()
    {
        if (!EditOptedIn() || !IsContextReadOptedIn()) return S_OK;
        Microsoft::WRL::ComPtr<ITfKeystrokeMgr> keys;
        HRESULT hr = m_threadMgr->QueryInterface(IID_PPV_ARGS(&keys));
        if (FAILED(hr)) return hr;
        hr = keys->AdviseKeyEventSink(m_clientId, static_cast<ITfKeyEventSink*>(this), TRUE);
        if (FAILED(hr)) return hr;
        m_keySink = true;
        hr = keys->PreserveKey(m_clientId, GUID_PCWordCaptureKey, &kCaptureKey, nullptr, 0);
        if (FAILED(hr)) return hr;
        m_captureKey = true;
        hr = keys->PreserveKey(m_clientId, GUID_PCWordApplyKey, &kApplyKey, nullptr, 0);
        if (FAILED(hr)) return hr;
        m_applyKey = true;
        return S_OK;
    }

    void UnregisterExperimentKeys()
    {
        if (!m_threadMgr) return;
        Microsoft::WRL::ComPtr<ITfKeystrokeMgr> keys;
        if (SUCCEEDED(m_threadMgr->QueryInterface(IID_PPV_ARGS(&keys))))
        {
            if (m_captureKey) keys->UnpreserveKey(GUID_PCWordCaptureKey, &kCaptureKey);
            if (m_applyKey) keys->UnpreserveKey(GUID_PCWordApplyKey, &kApplyKey);
            if (m_keySink) keys->UnadviseKeyEventSink(m_clientId);
        }
        m_keySink = m_captureKey = m_applyKey = false;
    }

    bool EligibleSelection(ITfContext* context, TfEditCookie cookie,
        Microsoft::WRL::ComPtr<ITfRange>& caret)
    {
        if (!EditOptedIn() || !IsContextReadOptedIn() || !HasCurrentFocus(context) ||
            !IsOrdinaryIntegrity()) return false;
        ExternalSettings settings;
        if (!ReadExternalSettings(&settings) || !settings.enabled ||
            !pcword::input::IsHostAllowlisted(settings.allowlist, GetHostExecutableName()) ||
            _wcsicmp(GetHostExecutableName().c_str(), L"notepad.exe") != 0) return false;
        TF_STATUS status{};
        if (context->GetStatus(&status) != S_OK ||
            (status.dwDynamicFlags & TF_SD_READONLY) != 0) return false;
        TF_SELECTION selection{};
        ULONG fetched = 0;
        const HRESULT hr = context->GetSelection(cookie, TF_DEFAULT_SELECTION, 1, &selection, &fetched);
        caret.Attach(selection.range);
        BOOL empty = FALSE;
        bool known = false;
        if (hr != S_OK || fetched != 1 || !caret || selection.style.fInterimChar ||
            caret->IsEmpty(cookie, &empty) != S_OK || !empty ||
            IsComposing(context, &known) || !known) return false;
        const wchar_t* stage = nullptr;
        HRESULT scopeHr = E_FAIL;
        const auto classification = GetInputScopeStatus(context, cookie, caret.Get(), &stage, &scopeHr);
        // Unknown is permitted only in this explicitly opted-in synthetic Notepad
        // experiment. This is not a production sensitive-field classification.
        if (classification == pcword::input::FieldClassification::Sensitive) return false;
        Microsoft::WRL::ComPtr<ITfContextView> view;
        RECT rect{};
        BOOL clipped = TRUE;
        return context->GetActiveView(&view) == S_OK && view &&
            view->GetTextExt(cookie, caret.Get(), &rect, &clipped) == S_OK &&
            !clipped && rect.bottom > rect.top && rect.right >= rect.left;
    }

    HRESULT CaptureExperiment(ITfContext* context, TfEditCookie cookie)
    {
        ClearCapture();
        Microsoft::WRL::ComPtr<ITfRange> caret;
        pcword::edit::Window window;
        if (!EligibleSelection(context, cookie, caret) ||
            !pcword::edit::ReadWindow(cookie, caret.Get(), window) ||
            !pcword::edit::SyntheticTokenEligible(window) ||
            !pcword::edit::Fingerprint(window, m_fingerprint) ||
            caret->Clone(&m_capturedCaret) != S_OK) return E_ACCESSDENIED;
        m_ticket = { m_generation, GetTickCount64(), true };
        Trace(L"experiment capture=armed ttl-ms=5000");
        return S_OK;
    }

    HRESULT ApplyExperiment(ITfContext* context, TfEditCookie cookie)
    {
        using namespace pcword::edit;
        ComPtr<ITfRange> caret, target, newCaret;
        Window window;
        std::wstring expected, observed;
        // All provider calls below execute under the granted write edit cookie.
        // A failed SetText/SetSelection/verification is uncertain, never retried.
        struct Backend
        {
            std::function<bool()> validate, replace, verify;
            bool Validate() { return validate(); }
            bool Replace() { return replace(); }
            bool Verify() { return verify(); }
        } backend{
            [&]() {
                std::array<UCHAR, 32> fingerprint{};
                if (!EligibleSelection(context, cookie, caret) ||
                    !SamePosition(cookie, caret.Get(), m_capturedCaret.Get()) ||
                    !ReadWindow(cookie, caret.Get(), window) || !SyntheticTokenEligible(window) ||
                    !Fingerprint(window, fingerprint) || fingerprint != m_fingerprint) return false;
                LONG moved = 0;
                if (caret->Clone(&target) != S_OK ||
                    target->ShiftStart(cookie, -static_cast<LONG>(std::size(ExpectedToken) - 1), &moved, nullptr) != S_OK ||
                    moved != -static_cast<LONG>(std::size(ExpectedToken) - 1)) return false;
                std::wstring token;
                if (!ReadRange(cookie, target.Get(), static_cast<ULONG>(std::size(ExpectedToken) - 1), token) ||
                    token != ExpectedToken) return false;
                expected = window.before.substr(0, window.before.size() - (std::size(ExpectedToken) - 1)) +
                    ReplacementToken + window.after;
                // Anchor the target across insertion, including the replacement.
                return target->SetGravity(cookie, TF_GRAVITY_BACKWARD, TF_GRAVITY_FORWARD) == S_OK;
            },
            [&]() {
                if (target->SetText(cookie, 0, ReplacementToken,
                    static_cast<LONG>(std::size(ReplacementToken) - 1)) != S_OK) return false;
                if (target->Clone(&newCaret) != S_OK ||
                    newCaret->Collapse(cookie, TF_ANCHOR_END) != S_OK) return false;
                TF_SELECTION selection{ newCaret.Get(), { TF_AE_NONE, FALSE } };
                return context->SetSelection(cookie, 1, &selection) == S_OK;
            },
            [&]() {
                ComPtr<ITfRange> actualCaret;
                if (!EligibleSelection(context, cookie, actualCaret) ||
                    !SamePosition(cookie, actualCaret.Get(), newCaret.Get()) ||
                    !ReadRange(cookie, target.Get(), static_cast<ULONG>(std::size(ReplacementToken) - 1), observed) ||
                    observed != ReplacementToken ||
                    !ReadRange(cookie, window.guard.Get(), static_cast<ULONG>(expected.size()), observed) ||
                    observed != expected) return false;
                return true;
            }
        };
        const Result result = Apply(m_ticket, m_generation, GetTickCount64(), backend);
        if (!expected.empty()) SecureZeroMemory(expected.data(), expected.size() * sizeof(wchar_t));
        if (!observed.empty()) SecureZeroMemory(observed.data(), observed.size() * sizeof(wchar_t));
        Trace(result == Result::Verified ? L"experiment edit=verified" :
            result == Result::Uncertain ? L"experiment edit=uncertain no-retry=yes" : L"experiment edit=rejected");
        return result == Result::Verified ? S_OK : E_FAIL;
    }

    HRESULT RunExperiment(ITfContext* context, bool write)
    {
        if (!context || context != m_context || m_inSession || !HasCurrentFocus(context))
        {
            ClearCapture();
            return E_ACCESSDENIED;
        }
        m_inSession = true;
        HRESULT hr = E_FAIL, sessionHr = E_FAIL;
        try
        {
            auto* session = new pcword::edit::EditSession(g_objects,
                [this, context, write](TfEditCookie cookie) {
                    return write ? ApplyExperiment(context, cookie) : CaptureExperiment(context, cookie);
                });
            // Synchronous requests are legal in a key callback. Never request a
            // write from OnEndEdit and never retry a denied/contended lock.
            hr = context->RequestEditSession(m_clientId, session,
                TF_ES_SYNC | (write ? TF_ES_READWRITE : TF_ES_READ), &sessionHr);
            session->Release();
        }
        catch (...) { hr = E_FAIL; }
        m_inSession = false;
        if (write || hr != S_OK || sessionHr != S_OK) ClearCapture();
        TraceHr(L"experiment request", hr);
        TraceHr(L"experiment session", sessionHr);
        return hr == S_OK ? sessionHr : hr;
    }
