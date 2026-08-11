class FastAPI:
    def get(self, route):
        return route

class httpx:
    @staticmethod
    def get(route):
        return route

class Celery:
    def task(self, function):
        return function

app = FastAPI()
worker = Celery()

@app.get("/not-a-framework-route")
@worker.task
def normalize_status(value):
    return httpx.get(value.strip().lower())
