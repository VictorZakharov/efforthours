package sample

import androidx.compose.runtime.Composable
import androidx.compose.material3.Text

data class Status(val ready: Boolean) {
    fun isReady(): Boolean = ready
}

@Composable
fun StatusScreen(status: Status) {
    Text(if (status.ready) "Ready" else "Waiting")
}
