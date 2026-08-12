#if defined(STATUS_A)
int select_status(int value) {
    if (value > 0) return 1;
    return 0;
}
#endif
