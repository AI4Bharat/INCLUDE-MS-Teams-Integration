import os
import json
import logging
import uvicorn
from dotenv import load_dotenv
from INCLUDE.evaluate import inference, get_inference_args
from fastapi import FastAPI, HTTPException
from azure.storage.blob import BlobServiceClient


logger = logging.getLogger("uvicorn.error")
app = FastAPI()
container_client = None


# You can specify whether to get the videos from blob storage or from local file system 
# with the "from_local" param.
@app.post('/inference')
async def root(file_name: str, local_file_path:str = "", from_local:bool = False):
    if all([from_local, file_name, local_file_path]):
        file_path = os.path.join(local_file_path, file_name)
    elif file_name and not from_local:
        if container_client is None:
            raise HTTPException(status_code=500, detail="Connection to azure storage is not configured.")
        blob_client = container_client.get_blob_client(file_name)
        file_path = os.path.join(path_to_downloaded_blobs, file_name)
        # Saving blob to local file system
        with open(file_path, "wb") as blob:
            blob.write(blob_client.download_blob().readall())  
    else:
        raise HTTPException(status_code=400, detail="Missing required parameters.")
    
    try:
        inference_args = get_inference_args([file_path])
        preds = inference(**inference_args)
        response = {
            "file": file_name,
            "predicted_label": preds[0]["predicted_label"]
        }
        logger.info("\nresult: \n" + json.dumps(response, indent=4))
        return response

    except AssertionError as error:
        logger.error(error)
        raise HTTPException(status_code=400, detail=error.args[0])

if __name__ == '__main__':
    # Load environment variables
    load_dotenv()

    # Set up for azure blob service client if credentials are in env vars
    az_storage_connection_string = os.environ.get("AZURE_STORAGE_CONNECTION_STRING")
    az_storage_container_name = os.environ.get("AZURE_STORAGE_CONTAINER_NAME")
    if az_storage_connection_string and az_storage_container_name:
        blob_service_client = BlobServiceClient.from_connection_string(az_storage_connection_string)
        container_client = blob_service_client.get_container_client(az_storage_container_name)

        # Path to the downloaded videos from blob when reading from azure storage
        path_to_downloaded_blobs = "downloaded"
        if not os.path.exists(path_to_downloaded_blobs):
            os.makedirs(path_to_downloaded_blobs)

    uvicorn.run("main:app", reload=True)
