#include "tcp_server.h"

#include <arpa/inet.h>
#include <netinet/in.h>
#include <sys/epoll.h>
#include <sys/socket.h>
#include <unistd.h>

#include <algorithm>
#include <cerrno>
#include <cstdio>
#include <cstring>
#include <stdexcept>

namespace net {

TcpServer::TcpServer(uint16_t port, int maxEvents) : port_(port), maxEvents_(maxEvents) {
    setupListenSocket();

    epollFd_ = epoll_create1(0);
    if (epollFd_ < 0) {
        throw std::runtime_error(std::string("epoll_create1 failed: ") + std::strerror(errno));
    }

    epoll_event ev{};
    ev.events = EPOLLIN;
    ev.data.fd = listenFd_;
    if (epoll_ctl(epollFd_, EPOLL_CTL_ADD, listenFd_, &ev) < 0) {
        throw std::runtime_error(std::string("epoll_ctl(listen) failed: ") + std::strerror(errno));
    }
}

TcpServer::~TcpServer() {
    for (auto& [fd, conn] : connections_) {
        ::close(fd);
    }
    if (listenFd_ >= 0) ::close(listenFd_);
    if (epollFd_ >= 0) ::close(epollFd_);
}

void TcpServer::setupListenSocket() {
    listenFd_ = ::socket(AF_INET, SOCK_STREAM | SOCK_NONBLOCK, 0);
    if (listenFd_ < 0) {
        throw std::runtime_error(std::string("socket() failed: ") + std::strerror(errno));
    }

    int reuse = 1;
    if (::setsockopt(listenFd_, SOL_SOCKET, SO_REUSEADDR, &reuse, sizeof(reuse)) < 0) {
        throw std::runtime_error(std::string("setsockopt(SO_REUSEADDR) failed: ") + std::strerror(errno));
    }

    sockaddr_in addr{};
    addr.sin_family = AF_INET;
    addr.sin_addr.s_addr = INADDR_ANY;
    addr.sin_port = htons(port_);

    if (::bind(listenFd_, reinterpret_cast<sockaddr*>(&addr), sizeof(addr)) < 0) {
        throw std::runtime_error(std::string("bind() failed: ") + std::strerror(errno));
    }
    if (::listen(listenFd_, 128) < 0) {
        throw std::runtime_error(std::string("listen() failed: ") + std::strerror(errno));
    }
}

void TcpServer::run() {
    running_ = true;
    std::vector<epoll_event> events(static_cast<size_t>(maxEvents_));

    std::printf("server listening on port %u\n", port_);

    while (running_) {
        int n = epoll_wait(epollFd_, events.data(), maxEvents_, 1000);
        if (n < 0) {
            if (errno == EINTR) continue;
            std::perror("epoll_wait");
            break;
        }

        for (int i = 0; i < n; ++i) {
            int fd = events[static_cast<size_t>(i)].data.fd;
            uint32_t flags = events[static_cast<size_t>(i)].events;

            if (fd == listenFd_) {
                acceptNewConnections();
                continue;
            }

            if (flags & (EPOLLHUP | EPOLLERR)) {
                closeConnection(fd);
                continue;
            }
            if (flags & EPOLLIN) {
                handleReadable(fd);
                // handleReadable() may have closed the connection already
                // (peer hangup / protocol error) -- don't touch it again.
                if (connections_.find(fd) == connections_.end()) continue;
            }
            if (flags & EPOLLOUT) {
                handleWritable(fd);
            }
        }
    }

    std::printf("server shutting down\n");
}

void TcpServer::stop() {
    running_ = false;
}

void TcpServer::acceptNewConnections() {
    for (;;) {
        sockaddr_in peerAddr{};
        socklen_t peerLen = sizeof(peerAddr);
        int clientFd = ::accept4(listenFd_, reinterpret_cast<sockaddr*>(&peerAddr), &peerLen, SOCK_NONBLOCK);
        if (clientFd < 0) {
            if (errno == EAGAIN || errno == EWOULDBLOCK) break;  // no more pending connections right now
            if (errno == EINTR) continue;
            std::perror("accept4");
            break;
        }

        epoll_event ev{};
        ev.events = EPOLLIN;
        ev.data.fd = clientFd;
        if (epoll_ctl(epollFd_, EPOLL_CTL_ADD, clientFd, &ev) < 0) {
            std::perror("epoll_ctl(ADD client)");
            ::close(clientFd);
            continue;
        }

        connections_.emplace(clientFd, std::make_unique<Connection>(clientFd));

        char ipStr[INET_ADDRSTRLEN] = {0};
        ::inet_ntop(AF_INET, &peerAddr.sin_addr, ipStr, sizeof(ipStr));
        std::printf("accepted connection fd=%d from %s:%u\n", clientFd, ipStr, ntohs(peerAddr.sin_port));
    }
}

void TcpServer::handleReadable(int fd) {
    auto it = connections_.find(fd);
    if (it == connections_.end()) return;
    Connection& conn = *it->second;

    uint8_t buffer[4096];
    for (;;) {
        ssize_t n = ::recv(fd, buffer, sizeof(buffer), 0);
        if (n > 0) {
            conn.appendReceived(buffer, static_cast<size_t>(n));
            continue;
        }
        if (n == 0) {
            closeConnection(fd);  // peer closed its end
            return;
        }
        if (errno == EAGAIN || errno == EWOULDBLOCK) break;  // drained the socket for now
        if (errno == EINTR) continue;
        closeConnection(fd);  // fatal read error
        return;
    }

    for (;;) {
        proto::Header header;
        std::vector<uint8_t> payload;
        FrameResult result = conn.tryExtractFrame(header, payload);
        if (result == FrameResult::Incomplete) break;
        if (result == FrameResult::ProtocolError) {
            std::printf("fd=%d sent an invalid frame (length=%u), closing\n", fd, header.length);
            closeConnection(fd);
            return;
        }
        dispatchFrame(conn, header, std::move(payload));
    }
}

void TcpServer::dispatchFrame(Connection& conn, const proto::Header& header, std::vector<uint8_t> payload) {
    switch (static_cast<proto::MessageType>(header.type)) {
        case proto::MessageType::EchoRequest: {
            proto::Header responseHeader;
            responseHeader.length = static_cast<uint32_t>(payload.size());
            responseHeader.sequence = header.sequence;
            responseHeader.type = static_cast<uint16_t>(proto::MessageType::EchoResponse);

            std::vector<uint8_t> frame(proto::HEADER_SIZE + payload.size());
            proto::encodeHeader(frame.data(), responseHeader);
            std::copy(payload.begin(), payload.end(), frame.begin() + static_cast<long>(proto::HEADER_SIZE));

            conn.queueSend(std::move(frame));

            bool wouldBlock = false;
            if (!conn.flushSend(wouldBlock)) {
                closeConnection(conn.fd());
                return;
            }
            if (wouldBlock || conn.hasPendingSend()) {
                epoll_event ev{};
                ev.events = EPOLLIN | EPOLLOUT;
                ev.data.fd = conn.fd();
                epoll_ctl(epollFd_, EPOLL_CTL_MOD, conn.fd(), &ev);
            }
            break;
        }
        default:
            std::printf("fd=%d sent unknown message type=%u, ignoring\n", conn.fd(), header.type);
            break;
    }
}

void TcpServer::handleWritable(int fd) {
    auto it = connections_.find(fd);
    if (it == connections_.end()) return;
    Connection& conn = *it->second;

    bool wouldBlock = false;
    if (!conn.flushSend(wouldBlock)) {
        closeConnection(fd);
        return;
    }

    if (!wouldBlock && !conn.hasPendingSend()) {
        // Fully drained -- stop asking for EPOLLOUT until there's something new to send.
        epoll_event ev{};
        ev.events = EPOLLIN;
        ev.data.fd = fd;
        epoll_ctl(epollFd_, EPOLL_CTL_MOD, fd, &ev);
    }
}

void TcpServer::closeConnection(int fd) {
    epoll_ctl(epollFd_, EPOLL_CTL_DEL, fd, nullptr);
    ::close(fd);
    connections_.erase(fd);
    std::printf("closed connection fd=%d\n", fd);
}

}  // namespace net
