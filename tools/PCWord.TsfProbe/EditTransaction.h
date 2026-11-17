#pragma once

#include <cstdint>

namespace pcword::edit
{
    enum class Result { Verified, Rejected, Uncertain };

    // A single-use capability. No document text or OS identifier is treated as
    // authorization by itself. The backend rechecks TSF identity under its lock.
    struct Ticket
    {
        std::uint64_t generation = 0;
        std::uint64_t capturedAt = 0;
        bool armed = false;
    };

    constexpr std::uint64_t LifetimeMilliseconds = 5000;

    template<class Backend>
    Result Apply(Ticket& ticket, std::uint64_t generation, std::uint64_t now, Backend& backend)
    {
        const bool eligible = ticket.armed && ticket.generation == generation &&
            now >= ticket.capturedAt && now - ticket.capturedAt <= LifetimeMilliseconds;
        // Consume even failed attempts. A failed provider call may have edited.
        ticket.armed = false;
        if (!eligible || !backend.Validate()) return Result::Rejected;
        if (!backend.Replace()) return Result::Uncertain;
        if (!backend.Verify()) return Result::Uncertain;
        return Result::Verified;
    }
}
