#!/usr/bin/env python3
"""纯标准库画的简易频谱图（无 numpy/Pillow 依赖）。

用法: python3 scripts/spectrogram.py docs/demo.wav docs/spectrogram.png [最高频率]
"""
import cmath
import math
import struct
import sys
import wave
import zlib


def read_wav(path):
    with wave.open(path, "rb") as w:
        sr = w.getframerate()
        raw = w.readframes(w.getnframes())
    return [v / 32768.0 for v in struct.unpack("<%dh" % (len(raw) // 2), raw)], sr


def fft(x):
    n = len(x)
    if n == 1:
        return x
    even = fft(x[0::2])
    odd = fft(x[1::2])
    out = [0j] * n
    for k in range(n // 2):
        t = cmath.exp(-2j * math.pi * k / n) * odd[k]
        out[k] = even[k] + t
        out[k + n // 2] = even[k] - t
    return out


def spectrogram(samples, sr, max_hz=4000, width=800, height=300):
    frame = 1024
    hop = max(1, (len(samples) - frame) // width)
    win = [0.5 - 0.5 * math.cos(2 * math.pi * i / frame) for i in range(frame)]
    bins = max(2, int(max_hz / (sr / frame)))

    cols = []
    peak = 1e-9
    for c in range(width):
        start = c * hop
        if start + frame > len(samples):
            break
        spec = fft([samples[start + k] * win[k] for k in range(frame)])
        mag = [abs(spec[b]) for b in range(bins)]
        cols.append(mag)
        peak = max(peak, max(mag))

    rows = []
    for y in range(height):
        b = int((height - 1 - y) / (height - 1) * (bins - 1))
        row = []
        for x in range(len(cols)):
            v = cols[x][b] / peak
            # -80 dB..0 dB 映射到 0..1
            row.append(max(0.0, 1.0 + math.log10(v + 1e-6) / 4.0))
        rows.append(row)
    return rows


def color(v):
    if v < 0.25:
        t = v / 0.25
        return (int(20 * t), int(20 * t), int(60 + 140 * t))
    if v < 0.5:
        t = (v - 0.25) / 0.25
        return (int(20 + 20 * t), int(20 + 180 * t), int(200 - 40 * t))
    if v < 0.75:
        t = (v - 0.5) / 0.25
        return (int(40 + 200 * t), int(200 + 40 * t), int(160 - 120 * t))
    t = (v - 0.75) / 0.25
    return (int(240 + 15 * t), int(240 - 60 * t), int(40 - 40 * t))


def write_png(path, rows):
    h = len(rows)
    w = len(rows[0])
    raw = b"".join(b"\x00" + b"".join(bytes(color(v)) for v in row) for row in rows)

    def chunk(tag, data):
        return (
            struct.pack(">I", len(data))
            + tag
            + data
            + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
        )

    png = (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(raw, 6))
        + chunk(b"IEND", b"")
    )
    with open(path, "wb") as f:
        f.write(png)
    print(f"{path}: {w}x{h}, {len(png)} bytes")


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        raise SystemExit(2)
    samples, sr = read_wav(sys.argv[1])
    max_hz = int(sys.argv[3]) if len(sys.argv) > 3 else 4000
    write_png(sys.argv[2], spectrogram(samples, sr, max_hz=max_hz))


if __name__ == "__main__":
    main()