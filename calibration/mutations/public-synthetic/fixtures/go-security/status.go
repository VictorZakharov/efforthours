package status

import "github.com/golang-jwt/jwt/v5"

func Ready() bool { return true }

func Token() *jwt.Token {
	return jwt.New(jwt.SigningMethodHS256)
}
