"""Serve the unchanged static app for browser tests, including concurrent PWA caching."""

import sys
from time import perf_counter

_started = perf_counter()
if __name__ == "__main__":
    print(f"Initialising browser fixtures with Python {sys.version.split()[0]}.", flush=True)

import argparse
import functools
import http.server
import socketserver
from pathlib import Path


class TestServer(http.server.ThreadingHTTPServer):
    # Python 3.12 defaults to five pending connections. A cold PWA requests many modules.
    request_queue_size = 128

    def server_bind(self):
        # HTTPServer normally performs reverse DNS here; loopback fixture hosting needs no name lookup.
        socketserver.TCPServer.server_bind(self)
        self.server_name, self.server_port = self.server_address[:2]


class StaticHandler(http.server.SimpleHTTPRequestHandler):
    # Reuse sockets instead of opening a new connection for every small ESM/vendor file.
    protocol_version = "HTTP/1.1"
    timeout = 30

    def log_request(self, code="-", size="-"):
        if isinstance(code, int) and code >= 400:
            super().log_request(code, size)


if __name__ == "__main__":
    print(f"HTTP fixture modules loaded in {perf_counter() - _started:.3f} seconds.", flush=True)
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=4173)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    handler = functools.partial(StaticHandler, directory=str(root))
    with TestServer(("127.0.0.1", args.port), handler) as server:
        print(f"Serving browser test fixtures at http://127.0.0.1:{args.port}", flush=True)
        server.serve_forever()
