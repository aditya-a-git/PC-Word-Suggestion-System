// Deterministic COM range/provider model. This exercises the production range
// operations, not a live Windows text store; real-host validation is separate.
#include "TsfRangeEdit.h"
#include "EditTransaction.h"
#include <iostream>
#include <vector>

#define REQUIRE(condition) do { if (!(condition)) { std::cerr << "failed at line " << __LINE__ << "\n"; return __LINE__; } } while (false)
using namespace pcword::edit;
constexpr TfEditCookie Cookie = 42;
class Range;
struct Document
{
    std::wstring text = L"hello pcword world";
    std::vector<Range*> ranges;
    int writes = 0, reads = 0;
    bool denyWrite = false, partialWrite = false, corruptNeighbor = false, denyRead = false, noOpWrite = false;
};

class Range final : public ITfRange
{
public:
    Range(Document& document, LONG start, LONG end) : doc(document), first(start), last(end)
    { doc.ranges.push_back(this); }
    ~Range() { doc.ranges.erase(std::find(doc.ranges.begin(), doc.ranges.end(), this)); }
    Document& doc;
    LONG first, last;
    TfGravity firstGravity = TF_GRAVITY_BACKWARD, lastGravity = TF_GRAVITY_FORWARD;
    STDMETHODIMP QueryInterface(REFIID iid, void** output) override
    {
        if (!output) return E_POINTER;
        *output = nullptr;
        if (iid != IID_IUnknown && iid != IID_ITfRange) return E_NOINTERFACE;
        *output = static_cast<ITfRange*>(this); AddRef(); return S_OK;
    }
    STDMETHODIMP_(ULONG) AddRef() override { return ++refs; }
    STDMETHODIMP_(ULONG) Release() override { ULONG count = --refs; if (!count) delete this; return count; }
    STDMETHODIMP Clone(ITfRange** output) override
    {
        auto* clone = new Range(doc, first, last);
        clone->firstGravity = firstGravity; clone->lastGravity = lastGravity;
        *output = clone; return S_OK;
    }
    STDMETHODIMP GetText(TfEditCookie cookie, DWORD, WCHAR* output, ULONG max, ULONG* count) override
    {
        if (cookie != Cookie) return TF_E_NOLOCK;
        ++doc.reads;
        if (doc.denyRead) return E_FAIL;
        *count = std::min(max, static_cast<ULONG>(last - first));
        std::copy_n(doc.text.data() + first, *count, output); return S_OK;
    }
    STDMETHODIMP SetText(TfEditCookie cookie, DWORD, const WCHAR* text, LONG length) override
    {
        if (cookie != Cookie) return TF_E_NOLOCK;
        ++doc.writes;
        if (doc.denyWrite) return TF_E_LOCKED;
        if (doc.noOpWrite) return S_OK;
        const LONG start = first, end = last, delta = length - (end - start);
        doc.text.replace(static_cast<size_t>(start), static_cast<size_t>(end - start), text, length);
        for (auto* range : doc.ranges)
        {
            auto adjust = [&](LONG position, TfGravity gravity) {
                if (position < start) return position;
                if (position > end) return position + delta;
                return gravity == TF_GRAVITY_BACKWARD ? start : start + length;
            };
            range->first = adjust(range->first, range->firstGravity);
            range->last = adjust(range->last, range->lastGravity);
        }
        first = start; last = start + length;
        if (doc.corruptNeighbor) doc.text[0] = L'X';
        return doc.partialWrite ? E_FAIL : S_OK;
    }
    STDMETHODIMP ShiftStart(TfEditCookie cookie, LONG amount, LONG* moved, const TF_HALTCOND*) override
    {
        if (cookie != Cookie) return TF_E_NOLOCK;
        LONG next = std::clamp(first + amount, 0L, last);
        *moved = next - first; first = next; return S_OK;
    }
    STDMETHODIMP ShiftEnd(TfEditCookie cookie, LONG amount, LONG* moved, const TF_HALTCOND*) override
    {
        if (cookie != Cookie) return TF_E_NOLOCK;
        LONG next = std::clamp(last + amount, first, static_cast<LONG>(doc.text.size()));
        *moved = next - last; last = next; return S_OK;
    }
    STDMETHODIMP Collapse(TfEditCookie cookie, TfAnchor anchor) override
    { if (cookie != Cookie) return TF_E_NOLOCK; if (anchor == TF_ANCHOR_START) last = first; else first = last; return S_OK; }
    STDMETHODIMP IsEmpty(TfEditCookie cookie, BOOL* empty) override
    { if (cookie != Cookie) return TF_E_NOLOCK; *empty = first == last; return S_OK; }
    STDMETHODIMP CompareStart(TfEditCookie cookie, ITfRange* other, TfAnchor anchor, LONG* result) override
    { return Compare(cookie, first, other, anchor, result); }
    STDMETHODIMP CompareEnd(TfEditCookie cookie, ITfRange* other, TfAnchor anchor, LONG* result) override
    { return Compare(cookie, last, other, anchor, result); }
    STDMETHODIMP ShiftStartToRange(TfEditCookie cookie, ITfRange* other, TfAnchor anchor) override
    { if (cookie != Cookie) return TF_E_NOLOCK; first = Position(other, anchor); return S_OK; }
    STDMETHODIMP ShiftEndToRange(TfEditCookie cookie, ITfRange* other, TfAnchor anchor) override
    { if (cookie != Cookie) return TF_E_NOLOCK; last = Position(other, anchor); return S_OK; }
    STDMETHODIMP SetGravity(TfEditCookie cookie, TfGravity start, TfGravity end) override
    { if (cookie != Cookie) return TF_E_NOLOCK; firstGravity = start; lastGravity = end; return S_OK; }
    STDMETHODIMP GetGravity(TfGravity* start, TfGravity* end) override
    { *start = firstGravity; *end = lastGravity; return S_OK; }
    STDMETHODIMP GetFormattedText(TfEditCookie, IDataObject**) override { return E_NOTIMPL; }
    STDMETHODIMP GetEmbedded(TfEditCookie, REFGUID, REFIID, IUnknown**) override { return E_NOTIMPL; }
    STDMETHODIMP InsertEmbedded(TfEditCookie, DWORD, IDataObject*) override { return E_NOTIMPL; }
    STDMETHODIMP ShiftStartRegion(TfEditCookie, TfShiftDir, BOOL*) override { return E_NOTIMPL; }
    STDMETHODIMP ShiftEndRegion(TfEditCookie, TfShiftDir, BOOL*) override { return E_NOTIMPL; }
    STDMETHODIMP IsEqualStart(TfEditCookie, ITfRange*, TfAnchor, BOOL*) override { return E_NOTIMPL; }
    STDMETHODIMP IsEqualEnd(TfEditCookie, ITfRange*, TfAnchor, BOOL*) override { return E_NOTIMPL; }
    STDMETHODIMP AdjustForInsert(TfEditCookie, ULONG, BOOL*) override { return E_NOTIMPL; }
    STDMETHODIMP GetContext(ITfContext**) override { return E_NOTIMPL; }
private:
    ULONG refs = 1;
    static LONG Position(ITfRange* range, TfAnchor anchor)
    { auto* typed = static_cast<Range*>(range); return anchor == TF_ANCHOR_START ? typed->first : typed->last; }
    HRESULT Compare(TfEditCookie cookie, LONG position, ITfRange* other, TfAnchor anchor, LONG* result)
    {
        if (cookie != Cookie) return TF_E_NOLOCK;
        if (&static_cast<Range*>(other)->doc != &doc) return E_INVALIDARG;
        *result = position - Position(other, anchor); return S_OK;
    }
};

int main()
{
    // Failures: policy/focus, selection, expected text, write lock, partial write,
    // neighboring corruption, failed selection, incorrect caret, read failure.
    for (int failure = 0; failure <= 12; ++failure)
    {
        Document doc;
        {
            ComPtr<ITfRange> caret; caret.Attach(new Range(doc, 12, 12));
            ComPtr<ITfRange> captured; REQUIRE(caret->Clone(&captured) == S_OK);
            Window window;
            std::array<UCHAR, 32> hash{};
            REQUIRE(ReadWindow(Cookie, caret.Get(), window));
            REQUIRE(Fingerprint(window, hash));
            if (failure == 2) caret.Attach(new Range(doc, 6, 6));
            if (failure == 3) doc.text[0] = L'X';
            doc.denyWrite = failure == 4;
            doc.partialWrite = failure == 5;
            doc.corruptNeighbor = failure == 6;
            doc.denyRead = failure == 9;
            doc.noOpWrite = failure == 12;
            int eligibilityCalls = 0;
            RangeEdit edit(Cookie, captured.Get(), hash,
                [&](ComPtr<ITfRange>& output) {
                    ++eligibilityCalls;
                    output = caret;
                    return failure != 1 && !(failure == 10 && eligibilityCalls == 2) &&
                        !(failure == 11 && eligibilityCalls == 3);
                },
                [&](ITfRange* requested) {
                    if (failure == 7) return false;
                    if (failure == 8) { caret.Attach(new Range(doc, 0, 0)); return true; }
                    return requested->Clone(caret.ReleaseAndGetAddressOf()) == S_OK;
                });
            Ticket ticket{ 1, 100, true };
            const Result result = Apply(ticket, 1, 101, edit);
            REQUIRE(result == (failure == 0 ? Result::Verified :
                failure <= 3 || failure == 9 ? Result::Rejected : Result::Uncertain));
            const bool wrote = failure == 0 || (failure >= 4 && failure <= 8) || failure >= 11;
            REQUIRE(doc.writes == (wrote ? 1 : 0));
            if (failure == 0)
            {
                REQUIRE(doc.text == L"hello pcword-verified world");
                REQUIRE(static_cast<Range*>(caret.Get())->first == 21);
            }
            const int writes = doc.writes;
            REQUIRE(Apply(ticket, 1, 102, edit) == Result::Rejected);
            REQUIRE(doc.writes == writes);
        }
        REQUIRE(doc.ranges.empty());
    }
    // Empty, multiline, supplementary/mixed scripts, truncated window boundary,
    // malformed UTF-16 at the caret, unavailable provider, and bounded long text.
    std::vector<std::wstring> texts = {
        L"", L"line one\r\n\x03B1\x03B2 \xD83D\xDE00 pcword",
        std::wstring(20, L'x') + L"\xD83D\xDE00" + std::wstring(127, L'x'),
        L"broken\xD800", std::wstring(10000, L'x')
    };
    for (size_t i = 0; i < texts.size(); ++i)
    {
        Document doc; doc.text = texts[i];
        ComPtr<ITfRange> caret;
        caret.Attach(new Range(doc, static_cast<LONG>(doc.text.size()), static_cast<LONG>(doc.text.size())));
        Window window;
        REQUIRE(ReadWindow(Cookie, caret.Get(), window) == (i != 3));
        if (i != 3) REQUIRE(window.before.size() <= BeforeLimit && WellFormed(window.before));
        if (i == 2) REQUIRE(window.before.size() == 127);
        Window noLock;
        REQUIRE(!ReadWindow(0, caret.Get(), noLock));
    }
    {
        Document doc;
        doc.text = std::wstring(31, L'x') + L"\xD83D\xDE00";
        ComPtr<ITfRange> caret; caret.Attach(new Range(doc, 0, 0));
        Window window;
        REQUIRE(ReadWindow(Cookie, caret.Get(), window));
        REQUIRE(window.after.size() == 31 && WellFormed(window.after));
    }
    return 0;
}
