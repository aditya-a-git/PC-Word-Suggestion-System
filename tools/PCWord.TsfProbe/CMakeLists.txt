cmake_minimum_required(VERSION 3.24)
project(PCWordTsfProbe LANGUAGES CXX)

if(NOT WIN32)
  message(FATAL_ERROR "PCWord.TsfProbe is Windows-only.")
endif()

add_library(PCWordTsfProbe SHARED TsfProbe.cpp PCWordTsfProbe.def)
target_compile_features(PCWordTsfProbe PRIVATE cxx_std_17)
target_compile_definitions(PCWordTsfProbe PRIVATE UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)
target_link_libraries(PCWordTsfProbe PRIVATE ole32 advapi32 uuid)
set_target_properties(PCWordTsfProbe PROPERTIES
  OUTPUT_NAME "PCWordTsfProbe"
  CXX_EXTENSIONS OFF
)

include(CTest)
if(BUILD_TESTING)
  add_executable(InputSafetyPolicyTests tests/InputSafetyPolicyTests.cpp)
  target_compile_features(InputSafetyPolicyTests PRIVATE cxx_std_17)
  add_test(NAME InputSafetyPolicyTests COMMAND InputSafetyPolicyTests)
endif()
