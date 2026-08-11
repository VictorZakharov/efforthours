package health;

import org.springframework.scheduling.annotation.Scheduled;

public final class Status {
    @Scheduled(cron = "0 * * * * *")
    public boolean ready() { return true; }
}
