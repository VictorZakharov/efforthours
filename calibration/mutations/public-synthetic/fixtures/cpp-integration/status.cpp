#include <curl/curl.h>

bool ready(int value) {
    return value > 0;
}

int perform(CURL* client) {
    return client == nullptr ? 1 : 0;
}
