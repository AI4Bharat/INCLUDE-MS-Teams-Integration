# How to run the Inference API

## Pre-requisites
1. `pip install requirements.txt`
2. Optional: If the bot is going to be on different servers, the video should be stored as Azure Blob.  
   Create `.env` file for azure storage credentials if needed. Please refer to template.env.
3. Ensure that the exposed port is open

## Steps

1. Run the following command to start FastAPI:
      ```
      python main.py
      ```
2. Sample request:  
  Reading videos from local file system:
  ```
  http://127.0.0.1:8000/inference?from_local=True&file_name=<your_file_name>&local_file_path=<your_path_to_file>
  ```
  Reading videos from azure storage:
  ```
  http://127.0.0.1:8000/inference?file_name=<your_file_name>
  ```

## Notes

- The inference code is forked from the original [INCLUDE repo](https://github.com/AI4Bharat/INCLUDE)
  - The model works by extracting pose keypoints from video using MediaPipe and using a classifier to find the signed word.
- The default MediaPipe installation works only on CPU.
  - For GPU build, check the `HowToUseMediaPipeWithGPU.md` file
