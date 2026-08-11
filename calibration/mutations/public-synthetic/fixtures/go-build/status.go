package status

import "embed"

//go:embed static/*
var static embed.FS

func Ready() bool { return true }
