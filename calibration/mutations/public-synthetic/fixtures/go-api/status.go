package status

import "net/http"

func Ready() bool { return true }

func Register() {
	http.HandleFunc("/status", func(http.ResponseWriter, *http.Request) {})
}
