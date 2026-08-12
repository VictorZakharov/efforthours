package sample

// Same represented behavior with deliberately different layout.
data class Status(
    val ready: Boolean,
)
{
    fun isReady( ): Boolean
        = ready
}
