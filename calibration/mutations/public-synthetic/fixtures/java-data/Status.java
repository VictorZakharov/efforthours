package health;

import org.springframework.jdbc.core.JdbcTemplate;

public final class Status {
    private JdbcTemplate jdbc;
    public boolean ready() { jdbc.query("select 1", null); return true; }
}
