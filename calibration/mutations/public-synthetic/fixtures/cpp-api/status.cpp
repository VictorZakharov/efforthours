#include <crow.h>

bool ready(int value) {
    return value > 0;
}

void route() {
    crow::SimpleApp server;
    server.route_dynamic("/status");
}
