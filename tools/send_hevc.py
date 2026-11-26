# Replays an Annex-B HEVC file as the RM UDP video link:
# 8-byte big-endian header (u16 frame id, u16 fragment index, u32 frame bytes) + <=1392 payload.
# Usage: python send_hevc.py <file.hevc> [host] [port] [fps] [seconds]
import socket, struct, sys, time

path = sys.argv[1]
host = sys.argv[2] if len(sys.argv) > 2 else "127.0.0.1"
port = int(sys.argv[3]) if len(sys.argv) > 3 else 3334
fps = float(sys.argv[4]) if len(sys.argv) > 4 else 60.0
seconds = float(sys.argv[5]) if len(sys.argv) > 5 else 20.0
data = open(path, "rb").read()

# Split into NAL units on start codes, then group into access units.
starts = []
i = 0
while True:
    j = data.find(b"\x00\x00\x01", i)
    if j < 0:
        break
    s = j - 1 if j > 0 and data[j - 1] == 0 else j
    starts.append((s, j + 3))
    i = j + 3
nals = []
for k, (s, body) in enumerate(starts):
    end = starts[k + 1][0] if k + 1 < len(starts) else len(data)
    nals.append((data[s:end], (data[body] >> 1) & 0x3F, data[body + 2] if body + 2 < len(data) else 0))
units, current, has_vcl = [], [], False
for raw, kind, first_byte in nals:
    vcl = kind < 32
    first_slice = vcl and (first_byte & 0x80) != 0
    starts_new = has_vcl and ((not vcl and kind in (32, 33, 34, 35, 39)) or first_slice)
    if starts_new:
        units.append(b"".join(current)); current, has_vcl = [], False
    current.append(raw); has_vcl = has_vcl or vcl
if current:
    units.append(b"".join(current))
print("access units:", len(units), "bytes:", len(data), flush=True)

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.setsockopt(socket.SOL_SOCKET, socket.SO_SNDBUF, 4 << 20)
frame_id, sent, deadline = 0, 0, time.perf_counter() + seconds
next_time = time.perf_counter()
while time.perf_counter() < deadline:
    for unit in units:
        if time.perf_counter() >= deadline:
            break
        total = len(unit)
        for index, offset in enumerate(range(0, total, 1392)):
            sock.sendto(struct.pack(">HHI", frame_id, index, total) + unit[offset:offset + 1392], (host, port))
        frame_id = (frame_id + 1) & 0xFFFF
        sent += 1
        next_time += 1.0 / fps
        delay = next_time - time.perf_counter()
        if delay > 0:
            time.sleep(delay)
print("frames sent:", sent, flush=True)
