# Unified Packet Protocol

## Problem

The current PGN-based protocol (inherited from AgOpenGPS/CAN bus) has drawbacks:
- Multiple message types with separate parsing/building logic
- Timing complexity between related messages
- Partial state updates cause race conditions
- Significant code overhead for marshalling

## Solution

Replace multiple PGNs with a single fixed-size packet exchanged at 50Hz.

## Packet Structure

```c
int16_t packet[512];  // 1024 bytes, all int16 - no packing issues
```

| Slots | Purpose |
|-------|---------|
| 0 | Header: sequence (low byte), flags (high byte) |
| 1 | Contract hash (CRC16 of slot definitions) |
| 2-511 | Data slots |

## Slot Assignment

Slots are assigned by documented convention, not struct field names:

```c
#define SLOT_HEADER         0
#define SLOT_HASH           1
#define SLOT_STEER_ANGLE    2   // int16: 0.01° resolution
#define SLOT_STEER_STATUS   3
#define SLOT_SPEED          4   // int16: cm/s
#define SLOT_XTE            5   // int16: mm
#define SLOT_SECTIONS_LO    6   // bits 0-15
#define SLOT_SECTIONS_HI    7   // bits 16-31
#define SLOT_GPS_LAT_LO     10  // int32 spans slots 10-11
#define SLOT_GPS_LAT_HI     11
// ... extend as needed
```

New features = new slot assignments. Packet format never changes.

## Self-Describing Contract

Every packet includes a contract hash. If a receiver sees an unknown hash:

1. Receiver sets `REQUEST_CONTRACT` flag in its outgoing packets
2. Sender responds with contract packet (slot definitions)
3. Receiver caches contract, resumes normal decoding

Fully async - join anytime, request contract if needed.

## Implementation

```c
// ESP32 - Send
memcpy(udpBuffer, packet, 1024);
sendto(sock, udpBuffer, 1024, ...);

// ESP32 - Receive
recvfrom(sock, udpBuffer, 1024, ...);
memcpy(packet, udpBuffer, 1024);
steerAngle = packet[SLOT_STEER_ANGLE];
```

```csharp
// C# - Send
Buffer.BlockCopy(packet, 0, udpBuffer, 0, 1024);
udpClient.Send(udpBuffer, 1024);

// C# - Receive
udpClient.Receive(udpBuffer);
Buffer.BlockCopy(udpBuffer, 0, packet, 0, 1024);
var steerAngle = packet[SLOT_STEER_ANGLE] * 0.01;
```

## Bandwidth

| Metric | Value |
|--------|-------|
| Packet size | 1 KB |
| Rate | 50 Hz |
| Throughput | 50 KB/s (400 kbps) |
| 10 Mbps utilization | 4% |
| 100 Mbps utilization | 0.4% |

## Benefits

| Aspect | Improvement |
|--------|-------------|
| Simplicity | memcpy in/out, no parsing |
| Atomicity | All state in one packet, no partial updates |
| Timing | Fixed 50Hz, predictable |
| Versioning | Add slots, never change format |
| Compatibility | Unknown slots ignored, backward/forward safe |
| Debugging | Wireshark dissector with contract file |
| Portability | int16 array has no packing/alignment issues |

## Comparison

| | PGN Protocol | Unified Packet |
|-|--------------|----------------|
| Message types | ~20 | 1 |
| Parsing code | Per-PGN handlers | memcpy |
| State consistency | Partial updates | Atomic |
| Add new data | New PGN + parser | New slot assignment |
| Bandwidth | ~5 KB/s | ~50 KB/s |
| Complexity | High | Low |
