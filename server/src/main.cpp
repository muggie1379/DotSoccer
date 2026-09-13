#if !defined(__linux__)
#error "This server uses Linux epoll (sys/epoll.h) and only builds on Linux. Build it from WSL or a native Linux environment."
#endif

#include <csignal>
#include <cstdio>
#include <cstdlib>

#include "tcp_server.h"

namespace {

net::TcpServer* g_server = nullptr;

void handleShutdownSignal(int /*signum*/) {
    if (g_server != nullptr) {
        g_server->stop();
    }
}

}  // namespace

int main(int argc, char** argv) {
    uint16_t port = 7777;
    if (argc > 1) {
        port = static_cast<uint16_t>(std::atoi(argv[1]));
    }

    try {
        net::TcpServer server(port);
        g_server = &server;

        std::signal(SIGINT, handleShutdownSignal);
        std::signal(SIGTERM, handleShutdownSignal);

        server.run();
    } catch (const std::exception& ex) {
        std::fprintf(stderr, "fatal: %s\n", ex.what());
        return 1;
    }

    return 0;
}
