package sample

import org.springframework.scheduling.annotation.Scheduled

data class Status(val ready: Boolean) {
    fun isReady(): Boolean = ready
}

@Scheduled(fixedDelay = 1000)
fun refreshStatus() = Unit
