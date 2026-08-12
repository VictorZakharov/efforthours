package sample

import io.ktor.server.routing.get

data class Status(val ready: Boolean) {
    fun isReady(): Boolean = ready
}

fun routes() {
    get("/status") { Status(true) }
}
