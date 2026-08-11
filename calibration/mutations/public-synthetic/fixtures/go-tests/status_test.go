package status

import "testing"

func TestReady(t *testing.T) {
	if !Ready() {
		t.Fatal("expected ready")
	}
}
