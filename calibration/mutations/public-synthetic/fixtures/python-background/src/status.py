from celery import Celery

app = Celery("status")

@app.task
def refresh_status():
    return "ok"
