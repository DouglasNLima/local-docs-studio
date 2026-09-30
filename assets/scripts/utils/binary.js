export function textToBase64(value) {
  return uint8ArrayToBase64(new TextEncoder().encode(value));
}

export function uint8ArrayToBase64(bytes) {
  const chunkSize = 0x8000;
  let binary = '';

  for (let index = 0; index < bytes.length; index += chunkSize) {
    binary += String.fromCharCode(...bytes.subarray(index, index + chunkSize));
  }

  return btoa(binary);
}

export function dataUrlToBlob(dataUrl) {
  const [meta, base64] = dataUrl.split(',');
  const mime = meta.match(/^data:([^;]+)/)?.[1] || 'application/octet-stream';
  return new Blob([base64ToUint8Array(base64 || '')], { type: mime });
}

export function base64ToUint8Array(value) {
  const binary = atob(value);
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index += 1) {
    bytes[index] = binary.charCodeAt(index);
  }
  return bytes;
}
