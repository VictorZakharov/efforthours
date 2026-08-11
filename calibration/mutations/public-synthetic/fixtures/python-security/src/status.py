from jose import jwt

def read_status(token, key):
    return jwt.decode(token, key, algorithms=["HS256"])
