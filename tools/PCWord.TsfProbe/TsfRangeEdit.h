#pragma once
#include "TsfRangeOperations.h"
#include <functional>
#include <utility>

namespace pcword::edit
{
    // Runs only inside a granted read/write edit session. Policy, focus and
    // selection acquisition remain the TIP's responsibility via eligible().
    class RangeEdit
    {
    public:
        RangeEdit(TfEditCookie cookie, ITfRange* captured, std::array<UCHAR, 32> fingerprint,
            std::function<bool(ComPtr<ITfRange>&)> eligible,
            std::function<bool(ITfRange*)> setSelection)
            : m_cookie(cookie), m_captured(captured), m_fingerprint(fingerprint),
              m_eligible(std::move(eligible)), m_setSelection(std::move(setSelection)) { }
        ~RangeEdit()
        {
            if (!m_expected.empty()) SecureZeroMemory(m_expected.data(), m_expected.size() * sizeof(wchar_t));
            if (!m_observed.empty()) SecureZeroMemory(m_observed.data(), m_observed.size() * sizeof(wchar_t));
        }
        bool Validate()
        {
            std::array<UCHAR, 32> fingerprint{};
            if (!m_eligible(m_caret) || !SamePosition(m_cookie, m_caret.Get(), m_captured.Get()) ||
                !ReadWindow(m_cookie, m_caret.Get(), m_window) || !SyntheticTokenEligible(m_window) ||
                !Fingerprint(m_window, fingerprint) || fingerprint != m_fingerprint) return false;
            LONG moved = 0;
            if (m_caret->Clone(&m_target) != S_OK ||
                m_target->ShiftStart(m_cookie, -static_cast<LONG>(std::size(ExpectedToken) - 1), &moved, nullptr) != S_OK ||
                moved != -static_cast<LONG>(std::size(ExpectedToken) - 1)) return false;
            if (!ReadRange(m_cookie, m_target.Get(), static_cast<ULONG>(std::size(ExpectedToken) - 1), m_observed) ||
                m_observed != ExpectedToken) return false;
            m_expected = m_window.before.substr(0, m_window.before.size() - (std::size(ExpectedToken) - 1)) +
                ReplacementToken + m_window.after;
            return m_target->SetGravity(m_cookie, TF_GRAVITY_BACKWARD, TF_GRAVITY_FORWARD) == S_OK;
        }
        bool Replace()
        {
            ComPtr<ITfRange> caret;
            if (!m_target || !m_eligible(caret) || !SamePosition(m_cookie, caret.Get(), m_caret.Get()))
                return false;
            if (m_target->SetText(m_cookie, 0, ReplacementToken,
                static_cast<LONG>(std::size(ReplacementToken) - 1)) != S_OK) return false;
            return m_target->Clone(&m_newCaret) == S_OK &&
                m_newCaret->Collapse(m_cookie, TF_ANCHOR_END) == S_OK && m_setSelection(m_newCaret.Get());
        }
        bool Verify()
        {
            ComPtr<ITfRange> caret;
            return m_eligible(caret) && SamePosition(m_cookie, caret.Get(), m_newCaret.Get()) &&
                ReadRange(m_cookie, m_target.Get(), static_cast<ULONG>(std::size(ReplacementToken) - 1), m_observed) &&
                m_observed == ReplacementToken &&
                ReadRange(m_cookie, m_window.guard.Get(), static_cast<ULONG>(m_expected.size()), m_observed) &&
                m_observed == m_expected;
        }
    private:
        TfEditCookie m_cookie;
        ComPtr<ITfRange> m_captured, m_caret, m_target, m_newCaret;
        std::array<UCHAR, 32> m_fingerprint;
        Window m_window;
        std::wstring m_expected, m_observed;
        std::function<bool(ComPtr<ITfRange>&)> m_eligible;
        std::function<bool(ITfRange*)> m_setSelection;
    };
}
