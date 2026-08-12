#include <openssl/evp.h>

bool ready(int value) {
    return value > 0;
}

bool verify(EVP_PKEY* key) {
    return key != nullptr;
}
