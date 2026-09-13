cmake_minimum_required(VERSION 3.16)
project(dotsoccer_server CXX)

if(NOT CMAKE_CXX_STANDARD)
    set(CMAKE_CXX_STANDARD 17)
    set(CMAKE_CXX_STANDARD_REQUIRED ON)
endif()

if(NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE Debug)
endif()

add_compile_options(-Wall -Wextra)

add_executable(server
    src/main.cpp
    src/tcp_server.cpp
    src/connection.cpp
)

target_include_directories(server PRIVATE include)
