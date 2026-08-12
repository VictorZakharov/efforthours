#include <sqlite3.h>

bool ready(int value) {
    return value > 0;
}

int query(sqlite3* database) {
    return database == nullptr ? 1 : 0;
}
