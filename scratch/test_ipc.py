import socket
import time

def test_ipc():
    host = "127.0.0.1"
    port = 5001
    print(f"Connecting to {host}:{port}...")
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            s.settimeout(5)
            s.connect((host, port))
            print("Connected! Waiting for data...")
            data = s.recv(1024).decode("utf-8")
            print(f"Received: {data}")
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    test_ipc()
