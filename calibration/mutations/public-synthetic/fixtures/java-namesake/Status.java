package health;

@GetMapping
public final class Status {
    public boolean ready() {
        HttpClient.send();
        JdbcTemplate.query();
        Jwts.builder();
        return true;
    }
}

@interface GetMapping { }
final class HttpClient { static void send() { } }
final class JdbcTemplate { static void query() { } }
final class Jwts { static void builder() { } }
