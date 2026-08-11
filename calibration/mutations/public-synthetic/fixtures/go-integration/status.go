package status

import "net/http"

func Ready() bool { return true }

func Refresh() error {
	_, err := http.Get("https://example.invalid/status")
	return err
}
