package sample

import okhttp3.OkHttpClient

data class Status(val ready: Boolean) {
    fun isReady(): Boolean = ready
}

fun fetch(client: OkHttpClient) {
    client.newCall(null)
}
