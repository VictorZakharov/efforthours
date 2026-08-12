#include "status.hpp"

bool extra_status() {
    return Status{}.ready(2);
}
