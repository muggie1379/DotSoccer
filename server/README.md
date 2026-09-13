# Server

Custom TCP game server, no middleware. C++17, Linux epoll, single-threaded
event loop for now (a thread pool / multi-reactor split is a later stage,
not stage 1).

## Stage 1 scope

- Accept TCP connections, buffer partial reads/writes correctly over the
  stream (a `recv()`/`send()` call is never guaranteed to align with frame
  boundaries).
- Parse the custom binary protocol described below.
- Echo back whatever `EchoRequest` payload it receives, tagged with the same
  sequence number, as an `EchoResponse`.

Nothing here has been compiled or run yet -- see [devlog.md](../devlog.md)
for current status. This machine has no C++ toolchain or WSL distro
installed, so the code needs to be built from a Linux environment (WSL,
a VM, or a container) before it can be tested.

## Protocol

Every message is a fixed 12-byte header followed by `length` bytes of
payload, all integers big-endian:

| offset | size | field     | meaning                                   |
|--------|------|-----------|--------------------------------------------|
| 0      | 4    | length    | payload size in bytes (header excluded)   |
| 4      | 4    | sequence  | per-connection counter, set by the sender |
| 8      | 2    | type      | `MessageType` (see `include/protocol.h`)  |
| 10     | 2    | reserved  | always 0 for now                          |

Message types so far:

- `1` `EchoRequest` -- client asks the server to echo `payload` back.
- `2` `EchoResponse` -- server's reply, same `sequence`, same `payload`.

`MAX_PAYLOAD_SIZE` (64 KiB) caps how large a single frame's payload can be;
a header claiming more than that is treated as a protocol error and the
connection is dropped.

## Building (from WSL / Linux)

```bash
sudo apt install -y build-essential cmake   # if not already installed
cmake -S server -B server/build
cmake --build server/build
```

## Running

```bash
./server/build/server 7777
```

## Testing manually

With the server running, from another shell (Python 3, no extra packages
needed):

```bash
python3 server/tools/echo_test_client.py 127.0.0.1 7777 5
```

This sends 5 `EchoRequest` frames with increasing sequence numbers and
checks that each `EchoResponse` matches.
