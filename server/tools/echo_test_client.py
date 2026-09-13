#!/usr/bin/env python3
"""Manual test client for the stage-1 echo server.

Connects over TCP, sends a handful of EchoRequest frames using the same
12-byte header format as server/include/protocol.h, and verifies that each
response comes back with the same sequence number and payload.

Usage:
    python3 echo_test_client.py [host] [port] [count]
"""

import socket
import struct
import sys

HEADER_FORMAT = ">IIHH"  # length, sequence, type, reserved (all big-endian)
HEADER_SIZE = struct.calcsize(HEADER_FORMAT)

MSG_ECHO_REQUEST = 1
MSG_ECHO_RESPONSE = 2


def recv_exact(sock: socket.socket, size: int) -> bytes:
    chunks = []
    remaining = size
    while remaining > 0:
        chunk = sock.recv(remaining)
        if not chunk:
            raise ConnectionError("server closed the connection early")
        chunks.append(chunk)
        remaining -= len(chunk)
    return b"".join(chunks)


def send_echo_request(sock: socket.socket, sequence: int, payload: bytes) -> None:
    header = struct.pack(HEADER_FORMAT, len(payload), sequence, MSG_ECHO_REQUEST, 0)
    sock.sendall(header + payload)


def read_frame(sock: socket.socket):
    header_bytes = recv_exact(sock, HEADER_SIZE)
    length, sequence, msg_type, _reserved = struct.unpack(HEADER_FORMAT, header_bytes)
    payload = recv_exact(sock, length) if length > 0 else b""
    return sequence, msg_type, payload


def main() -> int:
    host = sys.argv[1] if len(sys.argv) > 1 else "127.0.0.1"
    port = int(sys.argv[2]) if len(sys.argv) > 2 else 7777
    count = int(sys.argv[3]) if len(sys.argv) > 3 else 5

    with socket.create_connection((host, port), timeout=5) as sock:
        for seq in range(1, count + 1):
            payload = f"hello #{seq}".encode("utf-8")
            send_echo_request(sock, seq, payload)

            resp_seq, msg_type, resp_payload = read_frame(sock)

            ok = (
                msg_type == MSG_ECHO_RESPONSE
                and resp_seq == seq
                and resp_payload == payload
            )
            status = "OK" if ok else "MISMATCH"
            print(
                f"[{status}] sent seq={seq} payload={payload!r} "
                f"-> recv seq={resp_seq} type={msg_type} payload={resp_payload!r}"
            )
            if not ok:
                return 1

    print(f"all {count} echo round-trips succeeded")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
