#pragma once

#include <cstdint>
#include <memory>
#include <unordered_map>

#include "connection.h"

namespace net {

// A single-threaded, epoll-driven TCP server.
//
// Stage 1 behavior: accepts connections and echoes back every frame it
// receives with the same sequence number, wrapped in an EchoResponse header.
// The event loop / framing / partial-write handling here is written to carry
// forward unchanged into later stages (lobby, matchmaking, tick broadcast) --
// only the per-message dispatch in `dispatchFrame` grows.
class TcpServer {
public:
    explicit TcpServer(uint16_t port, int maxEvents = 64);
    ~TcpServer();

    TcpServer(const TcpServer&) = delete;
    TcpServer& operator=(const TcpServer&) = delete;

    // Runs the epoll event loop until stop() is called (typically from a
    // signal handler) or a fatal error occurs.
    void run();

    // Signals the event loop to exit after its current iteration.
    // Safe to call from a signal handler.
    void stop();

private:
    void setupListenSocket();
    void acceptNewConnections();
    void handleReadable(int fd);
    void handleWritable(int fd);
    void closeConnection(int fd);
    void dispatchFrame(Connection& conn, const proto::Header& header, std::vector<uint8_t> payload);

    uint16_t port_;
    int listenFd_ = -1;
    int epollFd_ = -1;
    int maxEvents_;
    volatile bool running_ = false;

    std::unordered_map<int, std::unique_ptr<Connection>> connections_;
};

}  // namespace net
