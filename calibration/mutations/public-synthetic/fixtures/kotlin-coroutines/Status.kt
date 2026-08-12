package sample

import kotlinx.coroutines.async
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow

data class Status(val ready: Boolean) {
    fun isReady(): Boolean = ready
}

suspend fun refresh(): Flow<Status> {
    async { Status(true) }
    return flow { }
}
