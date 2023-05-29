import logging
import socket
import sys

import requests

logging.basicConfig(level=logging.INFO)

port = int(sys.argv[1])

server_address = ('localhost', port)
with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
    sock.connect(server_address)

    while True:
        try:
            data = sock.recv(4096)
            if not data:
                # connection closed
                break


            # in this sample
            # read a url and request
            url = data.decode().strip()
            logging.info(f"Received URL: {url}")
            
            try:
                response = requests.get(url)
                if response.status_code == requests.codes.ok:
                    content = response.content
                    sock.sendall(content)
                    logging.info(f"Sent {len(content)} bytes of content back to the server")
                else:
                    content = b"Error requesting URL"
                    sock.sendall(content)
                    logging.error(f"Received error response (status code {response.status_code}) for URL: {url}")
            except requests.exceptions.RequestException as e:
                content = b"Error requesting URL"
                sock.sendall(content)
                logging.error(f"Error requesting URL: {url} - {e}")
        
        except socket.error as e:
            logging.error(f"Socket error: {e}")
            break
    
    sock.close()

