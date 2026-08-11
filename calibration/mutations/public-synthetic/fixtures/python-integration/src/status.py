import httpx

def remote_status():
    return httpx.get("https://example.invalid/status")
