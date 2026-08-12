package sample

annotation class Query(val value: String)
annotation class PreAuthorize(val value: String)
annotation class Composable

class HttpClient {
    fun newCall(value: Any?) = value
}

data class Status(val ready: Boolean) {
    fun isReady(): Boolean = ready
}

@Query("local")
@PreAuthorize("local")
@Composable
fun localNames(client: HttpClient) {
    client.newCall(null)
    get("/local") { }
}

fun get(path: String, block: () -> Unit) = block()
