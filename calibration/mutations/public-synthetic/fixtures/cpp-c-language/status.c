#include <stddef.h>

typedef struct Status {
    int value;
} Status;

int ready(const Status* status) {
    return status != NULL && status->value > 0;
}

int main(void) {
    Status status = { 1 };
    return ready(&status) ? 0 : 1;
}
