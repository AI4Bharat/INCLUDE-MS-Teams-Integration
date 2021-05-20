# How to run the inference api
  1. Create .env file for azure storage credentials if needed. Please refer to template.env.
  2. Run the following command to start FastAPI:
  ```
  uvicorn main:app --reload
  ```