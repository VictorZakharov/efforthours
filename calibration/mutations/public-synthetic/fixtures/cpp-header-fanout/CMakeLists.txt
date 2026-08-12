cmake_minimum_required(VERSION 3.20)
project(status LANGUAGES CXX)
add_library(status status.cpp extra.cpp)
target_include_directories(status PUBLIC include)
