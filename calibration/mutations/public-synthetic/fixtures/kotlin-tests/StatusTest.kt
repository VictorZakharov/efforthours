package sample

import kotlin.test.Test
import kotlin.test.assertTrue

class StatusTest {
    @Test
    fun readyStatusIsReady() {
        assertTrue(Status(true).isReady())
    }
}
