# How to run the inference api
  1. Create .env file for azure storage credentials if needed. Please refer to template.env.
  2. Run the following command to start FastAPI:
      ```
      python main.py
      ```
  3. Sample request
  Reading videos from local file system:
  ```
  http://127.0.0.1:8000/inference?from_local=True&file_name=<your_file_name>&file_path=<your_path_to_file>
  ```
  Reading videos from azure storage:
  ```
  http://127.0.0.1:8000/inference?file_name=<your_file_name>
  ```