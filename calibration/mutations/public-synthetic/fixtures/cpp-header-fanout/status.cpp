#include "status.hpp"

bool ready_status() {
    return Status{}.ready(1);
}
