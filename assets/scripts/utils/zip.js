const ZIP_MODULE_PATH = '../../vendor/fflate-0.8.2.browser.js';
const utf8Decoder = new TextDecoder();
let fflatePromise = null;

export function wrapBase64(value, lineLength = 76) {
  const lines = [];
  for (let index = 0; index < value.length; index += lineLength) {
    lines.push(value.slice(index, index + lineLength));
  }
  return lines.join('\r\n');
}

export async function readZipEntriesFromFile(file) {
  const bytes = new Uint8Array(await file.arrayBuffer());
  return readZipEntries(bytes);
}

export async function readZipEntries(bytes) {
  const { unzipSync } = await loadFflate();
  const entries = unzipSync(bytes);
  return new Map(Object.entries(entries));
}

export function decodeZipText(bytes) {
  return utf8Decoder.decode(bytes);
}

export function createZipBlob(files, mimeType) {
  const encoder = new TextEncoder();
  const localParts = [];
  const centralParts = [];
  let offset = 0;

  files.forEach((file) => {
    const nameBytes = encoder.encode(file.name);
    const dataBytes = file.data instanceof Uint8Array ? file.data : encoder.encode(file.data);
    const crc = crc32(dataBytes);
    const { time, date } = getDosDateTime();

    const localHeader = new ArrayBuffer(30);
    const local = new DataView(localHeader);
    local.setUint32(0, 0x04034b50, true);
    local.setUint16(4, 20, true);
    local.setUint16(6, 0x0800, true);
    local.setUint16(8, 0, true);
    local.setUint16(10, time, true);
    local.setUint16(12, date, true);
    local.setUint32(14, crc, true);
    local.setUint32(18, dataBytes.length, true);
    local.setUint32(22, dataBytes.length, true);
    local.setUint16(26, nameBytes.length, true);
    local.setUint16(28, 0, true);
    localParts.push(localHeader, nameBytes, dataBytes);

    const centralHeader = new ArrayBuffer(46);
    const central = new DataView(centralHeader);
    central.setUint32(0, 0x02014b50, true);
    central.setUint16(4, 20, true);
    central.setUint16(6, 20, true);
    central.setUint16(8, 0x0800, true);
    central.setUint16(10, 0, true);
    central.setUint16(12, time, true);
    central.setUint16(14, date, true);
    central.setUint32(16, crc, true);
    central.setUint32(20, dataBytes.length, true);
    central.setUint32(24, dataBytes.length, true);
    central.setUint16(28, nameBytes.length, true);
    central.setUint16(30, 0, true);
    central.setUint16(32, 0, true);
    central.setUint16(34, 0, true);
    central.setUint16(36, 0, true);
    central.setUint32(38, 0, true);
    central.setUint32(42, offset, true);
    centralParts.push(centralHeader, nameBytes);

    offset += localHeader.byteLength + nameBytes.length + dataBytes.length;
  });

  const centralSize = centralParts.reduce((sum, part) => sum + part.byteLength, 0);
  const endHeader = new ArrayBuffer(22);
  const end = new DataView(endHeader);
  end.setUint32(0, 0x06054b50, true);
  end.setUint16(8, files.length, true);
  end.setUint16(10, files.length, true);
  end.setUint32(12, centralSize, true);
  end.setUint32(16, offset, true);

  return new Blob([...localParts, ...centralParts, endHeader], { type: mimeType });
}

function crc32(bytes) {
  let crc = 0xffffffff;
  for (const byte of bytes) {
    let value = (crc ^ byte) & 0xff;
    for (let bit = 0; bit < 8; bit += 1) {
      value = value & 1 ? 0xedb88320 ^ (value >>> 1) : value >>> 1;
    }
    crc = (crc >>> 8) ^ value;
  }
  return (crc ^ 0xffffffff) >>> 0;
}

function getDosDateTime() {
  const now = new Date();
  const year = Math.max(now.getFullYear(), 1980);
  return {
    time: (now.getHours() << 11) | (now.getMinutes() << 5) | Math.floor(now.getSeconds() / 2),
    date: ((year - 1980) << 9) | ((now.getMonth() + 1) << 5) | now.getDate(),
  };
}

function loadFflate() {
  if (!fflatePromise) {
    fflatePromise = import(ZIP_MODULE_PATH);
  }

  return fflatePromise;
}
