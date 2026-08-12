#include <thread>

bool ready(int value) {
    return value > 0;
}

void run_async() {
    std::thread worker([] {});
    worker.join();
}
