package status

import "github.com/robfig/cron/v3"

func Ready() bool { return true }

func Schedule() {
	scheduler := cron.New()
	scheduler.AddFunc("@hourly", func() {})
}
