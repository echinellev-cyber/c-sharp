import socket
import sys

ip = "72.60.233.70"
port = 9009

print(f"Testing TCP connection to {ip}:{port}...")
try:
    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.settimeout(5)
    result = sock.connect_ex((ip, port))
    if result == 0:
        print("SUCCESS: Port is open and reachable.")
    else:
        print(f"FAILURE: Could not connect. Return code: {result} (0 means success)")
    sock.close()
except Exception as e:
    print(f"ERROR: {e}")
