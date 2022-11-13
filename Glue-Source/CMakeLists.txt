cmake_minimum_required(VERSION 3.24)
project(Glue_Source)

set(CMAKE_CXX_STANDARD 11)

add_library(TracyProfiler SHARED main.cpp tracy/public/TracyClient.cpp)
target_include_directories(TracyProfiler PRIVATE tracy/public)

target_compile_definitions(TracyProfiler PUBLIC
        TRACY_ENABLE
        TRACY_MANUAL_LIFETIME
        TRACY_DELAYED_INIT
        TRACY_NO_SAMPLING
        TRACY_NO_SYSTEM_TRACING
        TRACY_NO_EXIT
        TRACY_NO_CRASH_HANDLER)

set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ../Assemblies)
