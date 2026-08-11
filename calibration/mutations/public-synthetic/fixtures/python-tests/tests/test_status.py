from src.status import normalize_status

def test_normalize_status():
    assert normalize_status(" READY ") == "ready"
