#include "connection.h"

#include <sys/socket.h>
#include <unistd.h>

#include <algorithm>
#include <cerrno>

namespace net {

namespace {
// Once the consumed prefix of the send buffer grows past this, compact it
// away instead of letting the buffer grow unbounded.
constexpr size_t kSendBufferCompactionThreshold = 64 * 1024;
}  // namespace

Connection::Connection(int fd) : fd_(fd) {}

void Connection::appendReceived(const uint8_t* data, size_t len) {
    recvBuffer_.insert(recvBuffer_.end(), data, data + len);
}

FrameResult Connection::tryExtractFrame(proto::Header& outHeader, std::vector<uint8_t>& outPayload) {
    if (recvBuffer_.size() < proto::HEADER_SIZE) {
        return FrameResult::Incomplete;
    }

    proto::Header header = proto::decodeHeader(recvBuffer_.data());
    if (header.length > proto::MAX_PAYLOAD_SIZE) {
        return FrameResult::ProtocolError;
    }

    size_t totalSize = proto::HEADER_SIZE + header.length;
    if (recvBuffer_.size() < totalSize) {
        return FrameResult::Incomplete;
    }

    outHeader = header;
    outPayload.assign(recvBuffer_.begin() + proto::HEADER_SIZE, recvBuffer_.begin() + totalSize);
    recvBuffer_.erase(recvBuffer_.begin(), recvBuffer_.begin() + totalSize);
    return FrameResult::Ok;
}

void Connection::queueSend(std::vector<uint8_t> frame) {
    sendBuffer_.insert(sendBuffer_.end(), frame.begin(), frame.end());
}

bool Connection::flushSend(bool& wouldBlock) {
    wouldBlock = false;

    while (sendOffset_ < sendBuffer_.size()) {
        const uint8_t* data = sendBuffer_.data() + sendOffset_;
        size_t remaining = sendBuffer_.size() - sendOffset_;

        ssize_t written = ::send(fd_, data, remaining, MSG_NOSIGNAL);
        if (written > 0) {
            sendOffset_ += static_cast<size_t>(written);
            continue;
        }

        if (written < 0 && (errno == EAGAIN || errno == EWOULDBLOCK)) {
            wouldBlock = true;
            return true;
        }
        if (written < 0 && errno == EINTR) {
            continue;
        }
        return false;  // fatal socket error
    }

    if (sendOffset_ >= kSendBufferCompactionThreshold || sendOffset_ == sendBuffer_.size()) {
        sendBuffer_.erase(sendBuffer_.begin(), sendBuffer_.begin() + sendOffset_);
        sendOffset_ = 0;
    }

    return true;
}

}  // namespace net
