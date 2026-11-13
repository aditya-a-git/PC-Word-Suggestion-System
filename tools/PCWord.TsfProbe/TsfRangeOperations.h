#pragma once

#include <windows.h>
#include <msctf.h>
#include <bcrypt.h>
#include <wrl/client.h>
#include <array>
#include <string>
#include <algorithm>

namespace pcword::edit
{
    using Microsoft::WRL::ComPtr;
    constexpr LONG BeforeLimit = 128;
    constexpr LONG AfterLimit = 32;
    inline constexpr wchar_t ExpectedToken[] = L"pcword";
    inline constexpr wchar_t ReplacementToken[] = L"pcword-verified";

    inline bool WellFormed(const std::wstring& text)
    {
        for (size_t i = 0; i < text.size(); ++i)
        {
            const auto c = text[i];
            if (c == 0 || c == 0xFFFC) return false; // Embedded objects are unsupported.
            if (c >= 0xD800 && c <= 0xDBFF)
            {
                if (++i == text.size() || text[i] < 0xDC00 || text[i] > 0xDFFF) return false;
            }
            else if (c >= 0xDC00 && c <= 0xDFFF) return false;
        }
        return true;
    }

    inline bool ReadRange(TfEditCookie cookie, ITfRange* range, ULONG limit, std::wstring& text)
    {
        // One extra code unit detects a provider returning a longer range. Never
        // allocate from provider-supplied lengths or scan the complete document.
        if (!range || limit > BeforeLimit + AfterLimit + 32) return false;
        std::array<wchar_t, BeforeLimit + AfterLimit + 33> buffer{};
        ULONG count = 0;
        const HRESULT hr = range->GetText(cookie, 0, buffer.data(), limit + 1, &count);
        const bool valid = hr == S_OK && count <= limit;
        if (valid) text.assign(buffer.data(), count);
        SecureZeroMemory(buffer.data(), sizeof(buffer));
        return valid;
    }

    struct Window
    {
        ComPtr<ITfRange> beforeRange, afterRange, guard;
        std::wstring before, after;
        ~Window()
        {
            if (!before.empty()) SecureZeroMemory(before.data(), before.size() * sizeof(wchar_t));
            if (!after.empty()) SecureZeroMemory(after.data(), after.size() * sizeof(wchar_t));
        }
    };

    inline bool ReadWindow(TfEditCookie cookie, ITfRange* caret, Window& window)
    {
        LONG moved = 0;
        if (caret->Clone(&window.beforeRange) != S_OK ||
            window.beforeRange->Collapse(cookie, TF_ANCHOR_START) != S_OK ||
            window.beforeRange->ShiftStart(cookie, -BeforeLimit, &moved, nullptr) != S_OK ||
            moved > 0 || moved < -BeforeLimit ||
            !ReadRange(cookie, window.beforeRange.Get(), BeforeLimit, window.before)) return false;
        // A context window may cut a pair at its outer boundary. Trim only that
        // boundary, never a malformed pair at the insertion point.
        if (moved == -BeforeLimit && !window.before.empty() &&
            window.before.front() >= 0xDC00 && window.before.front() <= 0xDFFF)
        {
            if (window.beforeRange->ShiftStart(cookie, 1, &moved, nullptr) != S_OK || moved != 1) return false;
            window.before.erase(0, 1);
        }
        if (caret->Clone(&window.afterRange) != S_OK ||
            window.afterRange->Collapse(cookie, TF_ANCHOR_END) != S_OK ||
            window.afterRange->ShiftEnd(cookie, AfterLimit, &moved, nullptr) != S_OK ||
            moved < 0 || moved > AfterLimit ||
            !ReadRange(cookie, window.afterRange.Get(), AfterLimit, window.after)) return false;
        if (moved == AfterLimit && !window.after.empty() &&
            window.after.back() >= 0xD800 && window.after.back() <= 0xDBFF)
        {
            if (window.afterRange->ShiftEnd(cookie, -1, &moved, nullptr) != S_OK || moved != -1) return false;
            window.after.pop_back();
        }
        if (!WellFormed(window.before) || !WellFormed(window.after)) return false;
        if (window.beforeRange->Clone(&window.guard) != S_OK ||
            window.guard->ShiftEndToRange(cookie, window.afterRange.Get(), TF_ANCHOR_END) != S_OK ||
            window.guard->SetGravity(cookie, TF_GRAVITY_BACKWARD, TF_GRAVITY_FORWARD) != S_OK) return false;
        return true;
    }

    inline bool Fingerprint(const Window& window, std::array<UCHAR, 32>& digest)
    {
        std::array<wchar_t, BeforeLimit + AfterLimit + 1> buffer{};
        std::copy(window.before.begin(), window.before.end(), buffer.begin());
        std::copy(window.after.begin(), window.after.end(), buffer.begin() + window.before.size() + 1);
        BCRYPT_ALG_HANDLE algorithm = nullptr;
        NTSTATUS status = BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA256_ALGORITHM, nullptr, 0);
        if (status >= 0)
        {
            status = BCryptHash(algorithm, nullptr, 0, reinterpret_cast<PUCHAR>(buffer.data()),
                static_cast<ULONG>((window.before.size() + window.after.size() + 1) * sizeof(wchar_t)),
                digest.data(), static_cast<ULONG>(digest.size()));
            BCryptCloseAlgorithmProvider(algorithm, 0);
        }
        SecureZeroMemory(buffer.data(), sizeof(buffer));
        return status >= 0;
    }

    inline bool SyntheticTokenEligible(const Window& window)
    {
        constexpr size_t length = std::size(ExpectedToken) - 1;
        auto boundary = [](wchar_t c) { return c == L' ' || c == L'\r' || c == L'\n' || c == L'\t'; };
        return window.before.size() >= length &&
            window.before.compare(window.before.size() - length, length, ExpectedToken) == 0 &&
            (window.before.size() == length || boundary(window.before[window.before.size() - length - 1])) &&
            (window.after.empty() || boundary(window.after.front()));
    }

    inline bool SamePosition(TfEditCookie cookie, ITfRange* left, ITfRange* right)
    {
        LONG start = 1, end = 1;
        return left && right && left->CompareStart(cookie, right, TF_ANCHOR_START, &start) == S_OK &&
            left->CompareEnd(cookie, right, TF_ANCHOR_END, &end) == S_OK && start == 0 && end == 0;
    }
}
