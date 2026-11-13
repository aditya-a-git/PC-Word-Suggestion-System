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

# Explicitly separate identity/artifact; the read-only diagnostic TIP is retained.
add_library(PCWordTsfEditExperiment SHARED TsfProbe.cpp PCWordTsfEditExperiment.def)
target_compile_features(PCWordTsfEditExperiment PRIVATE cxx_std_17)
target_compile_definitions(PCWordTsfEditExperiment PRIVATE PCWORD_EDIT_EXPERIMENT
  UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)
target_compile_options(PCWordTsfEditExperiment PRIVATE /W4 /permissive- /sdl)
target_link_libraries(PCWordTsfEditExperiment PRIVATE ole32 oleaut32 advapi32 uuid bcrypt user32)
target_link_libraries(PCWordTsfProbe PRIVATE oleaut32 user32)

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

  foreach(test EditTransactionTests TsfEditSessionTests TsfRangeEditTests)
    add_executable(${test} ${test}.cpp)
    target_compile_features(${test} PRIVATE cxx_std_17)
    target_compile_definitions(${test} PRIVATE UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)
    target_compile_options(${test} PRIVATE /W4 /permissive- /sdl)
    target_link_libraries(${test} PRIVATE ole32 uuid bcrypt)
    add_test(NAME ${test} COMMAND ${test})
  endforeach()
endif()
