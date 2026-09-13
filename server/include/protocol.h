#pragma once

// Wire protocol for the game server.
//
// Every message on the wire is:
//   [ 12-byte header ][ payload, `length` bytes ]
//
// Header layout (all integers big-endian / network byte order):
//   offset 0  uint32  length     payload size in bytes (NOT including the header)
//   offset 4  uint32  sequence   monotonically increasing per-connection counter
//   offset 8  uint16  type       MessageType
//   offset 10 uint16  reserved   currently always 0, reserved for flags/versioning
//
// The header is packed/unpacked byte-by-byte instead of read as a C++ struct
// so wire format never depends on compiler struct padding or host endianness.

#include <arpa/inet.h>

#include <cstdint>
#include <cstring>

namespace proto {

constexpr size_t HEADER_SIZE = 12;

// Upper bound on a single frame's payload. Guards against a corrupt/malicious
// length field forcing an unbounded buffer allocation.
constexpr uint32_t MAX_PAYLOAD_SIZE = 64 * 1024;

enum class MessageType : uint16_t {
    EchoRequest = 1,
    EchoResponse = 2,
};

struct Header {
    uint32_t length = 0;
    uint32_t sequence = 0;
    uint16_t type = 0;
};

// Serializes a header into `out`, which must point at HEADER_SIZE writable bytes.
inline void encodeHeader(uint8_t* out, const Header& header) {
    uint32_t netLength = htonl(header.length);
    uint32_t netSequence = htonl(header.sequence);
    uint16_t netType = htons(header.type);
    uint16_t netReserved = 0;

    std::memcpy(out + 0, &netLength, sizeof(netLength));
    std::memcpy(out + 4, &netSequence, sizeof(netSequence));
    std::memcpy(out + 8, &netType, sizeof(netType));
    std::memcpy(out + 10, &netReserved, sizeof(netReserved));
}

// Parses a header from `in`, which must point at at least HEADER_SIZE readable bytes.
inline Header decodeHeader(const uint8_t* in) {
    uint32_t netLength;
    uint32_t netSequence;
    uint16_t netType;

    std::memcpy(&netLength, in + 0, sizeof(netLength));
    std::memcpy(&netSequence, in + 4, sizeof(netSequence));
    std::memcpy(&netType, in + 8, sizeof(netType));

    Header header;
    header.length = ntohl(netLength);
    header.sequence = ntohl(netSequence);
    header.type = ntohs(netType);
    return header;
}

}  // namespace proto
