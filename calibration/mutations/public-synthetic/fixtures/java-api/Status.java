package health;

import org.springframework.web.bind.annotation.GetMapping;

public final class Status {
    @GetMapping("/ready")
    public boolean ready() { return true; }
}
