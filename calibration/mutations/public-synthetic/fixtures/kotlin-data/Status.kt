package sample

import androidx.room.Query

data class Status(val ready: Boolean) {
    fun isReady(): Boolean = ready
}

interface StatusStore {
    @Query("select ready from status")
    fun load(): Boolean
}
