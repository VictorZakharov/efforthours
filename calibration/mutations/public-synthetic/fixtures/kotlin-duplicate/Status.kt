package sample

data class Status(val ready: Boolean) {
    fun isReady(): Boolean = ready
}
