use reqwest::Client;

pub async fn send(client: &Client) {
    let _response = client.get("https://example.invalid").send().await;
}
