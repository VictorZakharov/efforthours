package health;

import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.assertTrue;

final class StatusTest {
    @Test void isReady() { assertTrue(new Status().ready()); }
}
