package sample

import org.springframework.security.access.prepost.PreAuthorize

data class Status(val ready: Boolean) {
    fun isReady(): Boolean = ready
}

@PreAuthorize("hasRole('STATUS_READER')")
fun securedStatus(): Status = Status(true)
