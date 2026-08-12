#include <dlfcn.h>

bool ready(int value) {
    return value > 0;
}

extern "C" int status_code() {
    return dlopen("plugin.so", RTLD_NOW) == nullptr ? 1 : 0;
}
