import os
from dotenv import load_dotenv
from INCLUDE.evaluate import inference, get_inference_args
from fastapi import FastAPI
from azure.storage.blob import BlobServiceClient


# Load environment variables
load_dotenv()

# Set up for azure blob service client
az_storage_connection_string = os.environ["AZURE_STORAGE_CONNECTION_STRING"]
az_storage_container_name = os.environ["AZURE_STORAGE_CONTAINER_NAME"]
blob_service_client = BlobServiceClient.from_connection_string(az_storage_connection_string)
container_client = blob_service_client.get_container_client(az_storage_container_name)

# Path to the local videos generated from the bot
path_to_local_videos = os.environ["PATH_TO_LOCAL_VIDEOS"]
# Path to the downloaded videos from blob when reading from azure storage
path_to_downloaded_blobs = "downloaded"
if not os.path.exists(path_to_downloaded_blobs):
    os.makedirs(path_to_downloaded_blobs)

app = FastAPI()


# You can specify whether to get the videos from blob storage or from local file system 
# with the "from_local" param.
@app.post('/inference')
async def root(file_name: str, local_file_path:str = path_to_local_videos, from_local:bool = False):
    if from_local:
        file_path = os.path.join(local_file_path, file_name)
        print(file_path)
    else:
        blob_client = container_client.get_blob_client(file_name)
        file_path = os.path.join(path_to_downloaded_blobs, file_name)
        # Saving blob to local file system
        with open(file_path, "wb") as blob:
            blob.write(blob_client.download_blob().readall())

    inference_args = get_inference_args([file_path])
    preds = inference(**inference_args)
    response = {
        "file": file_name,
        "predicted_label": preds[0]["predicted_label"]
    }
    return response