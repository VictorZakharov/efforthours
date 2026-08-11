package status

import "sync"

func Ready() bool { return true }

func Parallel() <-chan bool {
	var wait sync.WaitGroup
	result := make(chan bool, 1)
	wait.Add(1)
	go func() {
		defer wait.Done()
		result <- Ready()
	}()
	wait.Wait()
	return result
}
