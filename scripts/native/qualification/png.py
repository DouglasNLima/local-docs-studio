"""Independent bounded PNG integrity/decoded BGRA verifier for retained WPF captures."""
import hashlib
import struct
import zlib


def inspect(data):
    if len(data) > 64 * 1024 * 1024 or data[:8] != b'\x89PNG\r\n\x1a\n': raise ValueError('Invalid/unbounded PNG')
    cursor = 8; chunks = []; image = bytearray(); header = None
    while cursor < len(data):
        if cursor + 12 > len(data): raise ValueError('Truncated PNG chunk')
        size = struct.unpack('>I', data[cursor:cursor+4])[0]; kind = data[cursor+4:cursor+8]
        end = cursor + 8 + size
        if end + 4 > len(data): raise ValueError('Truncated PNG payload')
        payload = data[cursor+8:end]
        if zlib.crc32(kind + payload) & 0xffffffff != struct.unpack('>I', data[end:end+4])[0]: raise ValueError('PNG CRC mismatch')
        if not chunks and kind != b'IHDR': raise ValueError('PNG header missing')
        if kind == b'IHDR':
            if header or size != 13: raise ValueError('Duplicate/invalid PNG header')
            header = struct.unpack('>IIBBBBB', payload)
        elif kind == b'IDAT': image.extend(payload)
        elif kind == b'IEND':
            if size or end + 4 != len(data): raise ValueError('PNG trailing data')
        elif kind[0] & 32 == 0: raise ValueError('Unsupported critical PNG chunk')
        chunks.append(kind); cursor = end + 4
    if not chunks or chunks[-1] != b'IEND' or not image: raise ValueError('Incomplete PNG')
    width, height, depth, colour, compression, filtering, interlace = header
    if not (0 < width <= 3840 and 0 < height <= 2160) or depth != 8 or colour not in (2, 6) or (compression, filtering, interlace) != (0, 0, 0):
        raise ValueError('Unsupported capture format/geometry')
    bpp = 4 if colour == 6 else 3; stride = width * bpp; expected = (stride + 1) * height
    decoder = zlib.decompressobj(); raw = decoder.decompress(image, expected + 1)
    if len(raw) != expected or not decoder.eof or decoder.unused_data or decoder.unconsumed_tail: raise ValueError('Invalid/truncated decoded PNG')
    previous = bytearray(stride); pixels = bytearray()
    def paeth(a, b, c):
        p = a + b - c; distances = [abs(p-a), abs(p-b), abs(p-c)]
        return (a, b, c)[distances.index(min(distances))]
    for y in range(height):
        offset = y * (stride+1); filt = raw[offset]; row = bytearray(raw[offset+1:offset+1+stride])
        if filt > 4: raise ValueError('Invalid PNG filter')
        for x in range(stride):
            left = row[x-bpp] if x >= bpp else 0; up = previous[x]; diagonal = previous[x-bpp] if x >= bpp else 0
            add = (0, left, up, (left+up)//2, paeth(left, up, diagonal))[filt]
            row[x] = (row[x] + add) & 255
        for x in range(0, stride, bpp): pixels.extend((row[x+2], row[x+1], row[x], row[x+3] if bpp == 4 else 255))
        previous = row
    return {'width': width, 'height': height, 'bgraSha256': hashlib.sha256(pixels).hexdigest()}
