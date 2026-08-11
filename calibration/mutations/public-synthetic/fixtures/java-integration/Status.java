package health;

import java.net.http.HttpClient;

public final class Status {
    public boolean ready() { HttpClient.newHttpClient().sendAsync(null, null); return true; }
}
