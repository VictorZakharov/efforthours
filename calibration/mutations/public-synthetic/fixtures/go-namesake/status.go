package status

type httpClient struct{}
func (httpClient) Get(string) {}

type database struct{}
func (database) Query(string) {}

type jwtToken struct{}
func (jwtToken) New() {}

func Ready() bool {
	httpClient{}.Get("local")
	database{}.Query("local")
	jwtToken{}.New()
	return true
}
