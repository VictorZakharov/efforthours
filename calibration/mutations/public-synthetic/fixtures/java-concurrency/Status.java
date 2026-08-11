package health;

import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public final class Status {
    public boolean ready() {
        ExecutorService pool = Executors.newSingleThreadExecutor();
        pool.submit(() -> true);
        return true;
    }
}
