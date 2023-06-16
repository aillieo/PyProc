import base64
import sys
import asyncio
import socket
from typing import Callable, Any, Optional

class CSHandler:
    _sock: Optional[socket.socket] = None
    _on_message: Optional[Callable[[bytes], None]] = None
    loop: Optional[asyncio.AbstractEventLoop] = None

    @classmethod
    def connect(cls, on_message: Callable[[bytes], None]) -> None:
        if not callable(on_message):
            raise ValueError("on_message must be a callable function.")

        try:
            port = int(sys.argv[1])
        except (IndexError, ValueError):
            raise ValueError("Port number not provided or invalid in command line arguments.")

        server_address = ('localhost', port)
        cls._sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        
        try:
            cls._sock.connect(server_address)
        except Exception as e:
            raise ConnectionError(f"Failed to connect to server: {e}")

        try:
            key = sys.argv[2]
        except IndexError:
            raise ValueError("Encryption key not provided in command line arguments.")

        cls.loop = asyncio.get_event_loop()
        cls.loop.create_task(cls._recv_loop())
        cls._on_message = on_message

        try:
            cls._sock.sendall(base64.b64decode(key))
        except Exception as e:
            raise IOError(f"Failed to send the encryption key: {e}")

    @classmethod
    def send(cls, data: bytes) -> None:
        if not isinstance(data, bytes):
            raise ValueError("Data must be of type bytes.")

        if cls._sock is None:
            raise ValueError("Socket not initialized. Call connect() first.")

        try:
            cls._sock.sendall(data)
        except Exception as e:
            raise IOError(f"Failed to send data: {e}")

    @classmethod
    async def _recv_loop(cls) -> None:
        if cls._sock is None:
            raise ValueError("Socket not initialized. Call connect() first.")

        while True:
            try:
                data = await cls.loop.run_in_executor(None, cls._sock.recv, 4096)
            except Exception as e:
                raise IOError(f"Failed to receive data: {e}")

            if not data:
                break

            cls._on_message(data)

    @classmethod
    def close(cls) -> None:
        if cls._sock is None:
            raise ValueError("Socket not initialized. Call connect() first.")

        cls._sock.close()