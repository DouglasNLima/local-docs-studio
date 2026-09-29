import { createReadStream } from 'node:fs';
import { stat } from 'node:fs/promises';
import { createServer } from 'node:http';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const port = Number(process.argv[2] || 4173);
const contentTypes = new Map([
  ['.css', 'text/css'],
  ['.html', 'text/html'],
  ['.ico', 'image/x-icon'],
  ['.js', 'text/javascript'],
  ['.json', 'application/json'],
  ['.mjs', 'text/javascript'],
  ['.png', 'image/png'],
  ['.svg', 'image/svg+xml'],
  ['.webmanifest', 'application/manifest+json'],
  ['.woff2', 'font/woff2'],
  ['.zip', 'application/zip'],
]);

createServer(async (request, response) => {
  if (!['GET', 'HEAD'].includes(request.method)) {
    response.writeHead(405).end();
    return;
  }

  let requestedPath;
  try {
    requestedPath = decodeURIComponent(new URL(request.url, 'http://127.0.0.1').pathname);
  } catch {
    response.writeHead(400).end();
    return;
  }

  if (requestedPath.includes('\\') || requestedPath.includes('\0')) {
    response.writeHead(400).end();
    return;
  }
  let filePath = path.resolve(root, `.${requestedPath}`);
  if (filePath !== root && !filePath.startsWith(`${root}${path.sep}`)) {
    response.writeHead(403).end();
    return;
  }

  try {
    let fileStat = await stat(filePath);
    if (fileStat.isDirectory()) {
      filePath = path.join(filePath, 'index.html');
      fileStat = await stat(filePath);
    }
    if (!fileStat.isFile()) throw new Error('Not a file');
    response.writeHead(200, {
      'Content-Type': `${contentTypes.get(path.extname(filePath)) || 'application/octet-stream'}; charset=utf-8`,
      'Content-Length': fileStat.size,
      'Cache-Control': 'no-store',
    });
    if (request.method === 'HEAD') response.end();
    else createReadStream(filePath).pipe(response);
  } catch {
    response.writeHead(404).end();
  }
}).listen(port, '127.0.0.1');
