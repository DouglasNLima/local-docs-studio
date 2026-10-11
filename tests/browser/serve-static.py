"""Serve the unchanged static app for browser tests, including concurrent PWA caching."""

import argparse
import functools
import http.server
from pathlib import Path


class TestServer(http.server.ThreadingHTTPServer):
    # Python 3.12 defaults to five pending connections. A cold PWA requests many modules.
    request_queue_size = 128


class StaticHandler(http.server.SimpleHTTPRequestHandler):
    # Reuse sockets instead of opening a new connection for every small ESM/vendor file.
    protocol_version = "HTTP/1.1"
    timeout = 30

    def log_request(self, code="-", size="-"):
        if isinstance(code, int) and code >= 400:
            super().log_request(code, size)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=4173)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    handler = functools.partial(StaticHandler, directory=str(root))
    with TestServer(("127.0.0.1", args.port), handler) as server:
        print(f"Serving browser test fixtures at http://127.0.0.1:{args.port}", flush=True)
        server.serve_forever()
