package health;

import io.jsonwebtoken.Jwts;

public final class Status {
    public boolean ready() { Jwts.builder().compact(); return true; }
}
