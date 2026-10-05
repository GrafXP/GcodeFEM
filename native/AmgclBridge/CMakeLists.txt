cmake_minimum_required(VERSION 3.20)
project(AmgclBridge CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
if(NOT CMAKE_BUILD_TYPE)
  set(CMAKE_BUILD_TYPE Release)
endif()

find_package(OpenMP REQUIRED)

add_library(AmgclBridge SHARED amgcl_bridge.cpp)
target_include_directories(AmgclBridge PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}/../third_party/amgcl)
target_compile_definitions(AmgclBridge PRIVATE AMGCL_NO_BOOST NOMINMAX _CRT_SECURE_NO_WARNINGS)
target_link_libraries(AmgclBridge PRIVATE OpenMP::OpenMP_CXX)
if(MSVC)
  target_compile_options(AmgclBridge PRIVATE /O2 /EHsc /bigobj /W3)
endif()
