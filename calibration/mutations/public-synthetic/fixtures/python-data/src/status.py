from sqlalchemy import select

def status_query(status_type):
    return select(status_type)
