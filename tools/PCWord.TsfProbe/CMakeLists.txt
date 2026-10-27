cmake_minimum_required(VERSION 3.24)
project(PCWordTsfProbe LANGUAGES CXX)

include(CTest)

if(NOT WIN32)
  message(FATAL_ERROR "PCWord.TsfProbe is Windows-only.")
endif()

add_library(PCWordTsfProbe SHARED TsfProbe.cpp PCWordTsfProbe.def)
target_compile_features(PCWordTsfProbe PRIVATE cxx_std_17)
target_compile_definitions(PCWordTsfProbe PRIVATE UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)
target_compile_options(PCWordTsfProbe PRIVATE /W4 /permissive- /sdl)
target_link_libraries(PCWordTsfProbe PRIVATE ole32 advapi32 uuid)
set_target_properties(PCWordTsfProbe PROPERTIES
  OUTPUT_NAME "PCWordTsfProbe"
  CXX_EXTENSIONS OFF
)

if(BUILD_TESTING)
  add_executable(InputScopePolicyTests InputScopePolicyTests.cpp)
  target_compile_features(InputScopePolicyTests PRIVATE cxx_std_17)
  target_compile_definitions(InputScopePolicyTests PRIVATE UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)
  target_compile_options(InputScopePolicyTests PRIVATE /W4 /permissive- /sdl)
  add_test(NAME InputScopePolicyTests COMMAND InputScopePolicyTests)

  add_executable(InputSafetyPolicyTests InputSafetyPolicyTests.cpp)
  target_compile_features(InputSafetyPolicyTests PRIVATE cxx_std_17)
  target_compile_definitions(InputSafetyPolicyTests PRIVATE UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)
  target_compile_options(InputSafetyPolicyTests PRIVATE /W4 /permissive- /sdl)
  add_test(NAME InputSafetyPolicyTests COMMAND InputSafetyPolicyTests)
endif()
