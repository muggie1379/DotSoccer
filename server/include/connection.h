#pragma once

#include <cstdint>
#include <vector>

#include "protocol.h"

namespace net {

enum class FrameResult {
    Incomplete,      // not enough bytes buffered yet for a full frame
    Ok,               // a full frame was extracted into the out-params
    ProtocolError,    // header claims an invalid/oversized length; connection should be dropped
};

// Wraps one client socket: buffers partial reads until a full protocol frame
// is available, and buffers outgoing bytes until the socket is writable.
//
// A single send()/recv() call is never guaranteed to move a whole frame's
// worth of bytes over a TCP stream, so both directions need explicit buffering.
class Connection {
public:
    explicit Connection(int fd);

    int fd() const { return fd_; }

    // Appends bytes just read from the socket into the receive buffer.
    void appendReceived(const uint8_t* data, size_t len);

    // Tries to pull one complete frame out of the receive buffer.
    // On FrameResult::Ok, `outHeader`/`outPayload` are filled and the frame's
    // bytes are consumed from the internal buffer.
    FrameResult tryExtractFrame(proto::Header& outHeader, std::vector<uint8_t>& outPayload);

    // Appends a fully-encoded frame to the outgoing queue.
    void queueSend(std::vector<uint8_t> frame);

    // Attempts to write as much of the outgoing queue as the socket accepts
    // right now. Returns false on an unrecoverable socket error (caller should
    // close the connection). Sets `wouldBlock` when the socket's send buffer
    // is full and the caller should wait for EPOLLOUT before retrying.
    bool flushSend(bool& wouldBlock);

    bool hasPendingSend() const { return sendOffset_ < sendBuffer_.size(); }

private:
    int fd_;
    std::vector<uint8_t> recvBuffer_;

    // sendBuffer_[sendOffset_:] is the data still waiting to go out. The
    // consumed prefix is compacted away periodically instead of on every
    // write so a steady stream of small sends doesn't need to shift the
    // buffer every time.
    std::vector<uint8_t> sendBuffer_;
    size_t sendOffset_ = 0;
};

}  // namespace net
