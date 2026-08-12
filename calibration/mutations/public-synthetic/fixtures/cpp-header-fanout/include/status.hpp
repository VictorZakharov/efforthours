#pragma once

struct Status {
    bool ready(int value) const {
        return value > 0;
    }
};
