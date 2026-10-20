#pragma once
#include <chrono>
#include <stdexcept>

// Production uses the D3D11 query and steady clock; fixtures supply bounded deterministic responses.
template<class Poll,class Now,class Pause>
void WaitForGpuCompletion(Poll poll,Now now,Pause pause) {
    auto start=now();
    while(true) {
        if(poll()) return;
        if(now()-start>std::chrono::seconds(2))
            throw std::runtime_error("GPU completion timeout; image not released");
        pause();
    }
}