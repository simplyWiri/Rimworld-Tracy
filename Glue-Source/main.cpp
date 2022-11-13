#include <ios>
#include <iostream>
#include <tracy/Tracy.hpp>
#include <tracy/TracyC.h>
#include <vector>
#include <stack>
#include <fstream>


using ZoneContext = ___tracy_c_zone_context;
using SourceLocation = ___tracy_source_location_data;

static std::stack<ZoneContext> zoneStack{};
static std::array<SourceLocation*, 512000> sourceLocations{};
static std::atomic<int> nextSourceLocationIndex{0};

static bool active = false;

extern "C" void BeginZone(int64_t zoneId) {
    if (!active) return;

    zoneStack.push(___tracy_emit_zone_begin(sourceLocations[zoneId], true));
}
extern "C" void EndZone() {
    if (!active) return;

    const auto& cz = zoneStack.top();
    ___tracy_emit_zone_end(cz);
    zoneStack.pop();
}

extern "C" int64_t RegisterSourceFunction(const char* function, int strLen) {
    auto* srcLocation = new SourceLocation();
    srcLocation->name = (const char*)malloc(strLen + 1);
    memcpy((void*)srcLocation->name, function, strLen);
    const_cast<char*>(srcLocation->name)[strLen] = 0;

    srcLocation->function = "";
    srcLocation->file = "";
    srcLocation->line = 0;
    srcLocation->color = 0;

    auto idx = atomic_fetch_add(&nextSourceLocationIndex, 1);
    sourceLocations[idx] = srcLocation;
    return idx;
}

extern "C" void Startup() {
    ___tracy_startup_profiler();
}

extern "C" void Shutdown() {
    ___tracy_shutdown_profiler();
}

extern "C" void ToggleActive() {
    if(active) {
        for(int i = 0; i < zoneStack.size(); i++) {
            EndZone();
        }
    }

    active = !active;
}