export module status;

export template<typename T>
concept Positive = requires(T value) { value > 0; };

export int normalize(Positive auto value) {
    return value > 0 ? 1 : 0;
}
