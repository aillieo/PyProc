import asyncio
import requests
import logging
from CSHandler import CSHandler

def handle_message(data):
    url = data.decode().strip()
    print(f"Received URL: {url}")
    logging.info(f"Received URL: {url}")
    
    try:
        response = requests.get(url)
        if response.status_code == requests.codes.ok:
            content = response.content
            CSHandler.send(content)
            logging.info(f"Sent {len(content)} bytes of content back to the server")
        else:
            content = b"Error requesting URL"
            CSHandler.send(content)
            logging.error(f"Received error response (status code {response.status_code}) for URL: {url}")
    except requests.exceptions.RequestException as e:
        content = b"Error requesting URL"
        CSHandler.send(content)
        logging.error(f"Error requesting URL: {url} - {e}")

async def main():
    logging.getLogger().setLevel(logging.DEBUG)

    CSHandler.connect(handle_message)

    await asyncio.sleep(5)

    CSHandler.close()

if __name__ == "__main__":
    asyncio.run(main())
