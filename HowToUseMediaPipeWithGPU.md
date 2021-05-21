# Introduction

This is a guide of how to run hands and poses of [mediapipe](https://google.github.io/mediapipe/) on GPU from the scratch. The output files will be videos not coordinates and it cannot work with `import mediapipe` in python.

## Environment Settings

1. Create a Azure virtual machine with GPU.
    * Size: Standard NC6s_v3 (or anything with GPU ...)
    * Networking > Inbound port rules: RDP, Port: 3389
    * (Optional) Extension: Install Nvidia driver
        * I didn't install this extension but installed all the things by myself.

1. ssh to the VM
    * `ssh -i <private key path> <username>@<ip>`

1. Install GUI on ubuntu
    * I think anything is okay. For my case, I installed xfce. Please check this [site](https://docs.microsoft.com/en-us/azure/virtual-machines/linux/use-remote-desktop).

1. Check which CUDA/CuDnn/Tensorflow version to use.
    * If you only want to run hands and poses, you can skip this step.
    * Go to [mediapipe/cuda_support](https://google.github.io/mediapipe/getting_started/gpu_support.html#tensorflow-cuda-support-and-setup-on-linux-desktop)
    * Check the line of `export PATH`.
        * `export PATH=/usr/local/cuda-10.1/bin${PATH:+:${PATH}}`
    * Check which version of CUDA it is using.
        * For my case, it is CUDA 10.1
    * Go to [this site](https://www.tensorflow.org/install/source#gpu) to find corresponding version for CUDA/Cudnn/Tensorflow.
        * For my case, CUDA: 10.1, cuDNN: 7.6, tensorflow: 2.2

1. Install CUDA
    * If you only want to run hands and poses, maybe you can skip this step?
    * Follow the guild line [here](https://docs.nvidia.com/cuda/archive/10.1/cuda-installation-guide-linux/index.html).
    * I installed nvidia driver by the run time file

1. Install CuDNN
    * If you only want to run hands and poses, maybe you can skip this step?
    * Follow the guild line [here](https://docs.nvidia.com/deeplearning/cudnn/install-guide/index.html#installlinux-deb).

1. Install tensorflow
    * `sudo apt install python3-dev python3-pip`
    * `pip3 install tensorflow==2.2`

1. Test if everything is okay
    * `python3 -c "import tensorflow as tf;print(tf.reduce_sum(tf.random.normal([1000, 1000])))"`
    * You should be able to see something like this ...

        ```bash
        2021-05-21 03:06:39.434701: I tensorflow/stream_executor/platform/default/dso_loader.cc:44] Successfully opened dynamic library libcuda.so.1
        2021-05-21 03:06:39.715635: I tensorflow/core/common_runtime/gpu/gpu_device.cc:1561] Found device 0 with properties:
        pciBusID: 0001:00:00.0 name: NVIDIA Tesla V100-PCIE-16GB computeCapability: 7.0
        coreClock: 1.38GHz coreCount: 80 deviceMemorySize: 15.78GiB deviceMemoryBandwidth: 836.37GiB/s
        2021-05-21 03:06:39.727641: I tensorflow/stream_executor/platform/default/dso_loader.cc:44] Successfully opened dynamic library libcudart.so.10.1
        2021-05-21 03:06:40.121188: I tensorflow/stream_executor/platform/default/dso_loader.cc:44] Successfully opened dynamic library libcublas.so.10
        2021-05-21 03:06:40.323133: I tensorflow/stream_executor/platform/default/dso_loader.cc:44] Successfully opened dynamic library libcufft.so.10
        2021-05-21 03:06:40.368203: I tensorflow/stream_executor/platform/default/dso_loader.cc:44] Successfully opened dynamic library libcurand.so.10
        2021-05-21 03:06:40.774471: I tensorflow/stream_executor/platform/default/dso_loader.cc:44] Successfully opened dynamic library libcusolver.so.10
        2021-05-21 03:06:40.815709: I tensorflow/stream_executor/platform/default/dso_loader.cc:44] Successfully opened dynamic library libcusparse.so.10
        2021-05-21 03:06:41.495450: I tensorflow/stream_executor/platform/default/dso_loader.cc:44] Successfully opened dynamic library libcudnn.so.7
        2021-05-21 03:06:41.497249: I tensorflow/core/common_runtime/gpu/gpu_device.cc:1703] Adding visible gpu devices: 0
        2021-05-21 03:06:41.502920: I tensorflow/core/platform/cpu_feature_guard.cc:143] Your CPU supports instructions that this TensorFlow binary was not compiled to use: AVX2 FMA
        2021-05-21 03:06:41.581156: I tensorflow/core/platform/profile_utils/cpu_utils.cc:102] CPU Frequency: 2593985000 Hz
        2021-05-21 03:06:41.582527: I tensorflow/compiler/xla/service/service.cc:168] XLA service 0x7f5730000b20 initialized for platform Host (this does not guarantee that XLA will be used). Devices:
        2021-05-21 03:06:41.582549: I tensorflow/compiler/xla/service/service.cc:176]   StreamExecutor device (0): Host, Default Version
        2021-05-21 03:06:42.016504: I tensorflow/compiler/xla/service/service.cc:168] XLA service 0x4f99850 initialized for platform CUDA (this does not guarantee that XLA will be used). Devices:
        2021-05-21 03:06:42.016547: I tensorflow/compiler/xla/service/service.cc:176]   StreamExecutor device (0): NVIDIA Tesla V100-PCIE-16GB, Compute Capability 7.0
        2021-05-21 03:06:42.023024: I tensorflow/core/common_runtime/gpu/gpu_device.cc:1561] Found device 0 with properties:
        pciBusID: 0001:00:00.0 name: NVIDIA Tesla V100-PCIE-16GB computeCapability: 7.0
        coreClock: 1.38GHz coreCount: 80 deviceMemorySize: 15.78GiB deviceMemoryBandwidth: 836.37GiB/s
        2021-05-21 03:06:42.023085: I tensorflow/stream_executor/platform/default/dso_loader.cc:44] Successfully opened dynamic library libcudart.so.10.1
        2021-05-21 03:06:42.023109: I tensorflow/stream_executor/platform/default/dso_loader.cc:44] Successfully opened dynamic library libcublas.so.10
        2021-05-21 03:06:42.023129: I tensorflow/stream_executor/platform/default/dso_loader.cc:44] Successfully opened dynamic library libcufft.so.10
        2021-05-21 03:06:42.023148: I tensorflow/stream_executor/platform/default/dso_loader.cc:44] Successfully opened dynamic library libcurand.so.10
        2021-05-21 03:06:42.023168: I tensorflow/stream_executor/platform/default/dso_loader.cc:44] Successfully opened dynamic library libcusolver.so.10
        2021-05-21 03:06:42.023186: I tensorflow/stream_executor/platform/default/dso_loader.cc:44] Successfully opened dynamic library libcusparse.so.10
        2021-05-21 03:06:42.023206: I tensorflow/stream_executor/platform/default/dso_loader.cc:44] Successfully opened dynamic library libcudnn.so.7
        2021-05-21 03:06:42.024861: I tensorflow/core/common_runtime/gpu/gpu_device.cc:1703] Adding visible gpu devices: 0
        2021-05-21 03:06:42.030519: I tensorflow/stream_executor/platform/default/dso_loader.cc:44] Successfully opened dynamic library libcudart.so.10.1
        2021-05-21 03:06:42.035108: I tensorflow/core/common_runtime/gpu/gpu_device.cc:1102] Device interconnect StreamExecutor with strength 1 edge matrix:
        2021-05-21 03:06:42.035134: I tensorflow/core/common_runtime/gpu/gpu_device.cc:1108]      0
        2021-05-21 03:06:42.035146: I tensorflow/core/common_runtime/gpu/gpu_device.cc:1121] 0:   N
        2021-05-21 03:06:42.042640: I tensorflow/core/common_runtime/gpu/gpu_device.cc:1247] Created TensorFlow device (/job:localhost/replica:0/task:0/device:GPU:0 with 14902 MB memory) -> physical GPU (device: 0, name: NVIDIA Tesla V100-PCIE-16GB, pci bus id: 0001:00:00.0, compute capability: 7.0)
        tf.Tensor(-1009.7802, shape=(), dtype=float32)
        ```

1. Clone mediapipe to the repository
    * `git clone https://github.com/google/mediapipe`

1. Setup openCV
    * `cd meidapipe`
    * `sh setup_opencv.sh`

1. Copy the test video to the VM
    * `scp -i <private key path> <test_video_path> <username>@<ip>:<location>`

1. [OpenGL ES Setup on Linux Desktop](https://google.github.io/mediapipe/getting_started/gpu_support.html#opengl-es-setup-on-linux-desktop)
    * `sudo apt-get install mesa-common-dev libegl1-mesa-dev libgles2-mesa-dev`
    * `sudo apt-get install mesa-utils`

1. Log in to the VM by remote desktop
1. Open a terminal
1. `glxinfo | grep -i opengl`
1. Check OpenGL version

    ```bash
    azureuser@tsutingTest:~$ glxinfo | grep -i opengl
    OpenGL vendor string: VMware, Inc.
    OpenGL renderer string: llvmpipe (LLVM 10.0.0, 256 bits)
    OpenGL core profile version string: 3.3 (Core Profile) Mesa 20.0.8
    OpenGL core profile shading language version string: 3.30
    OpenGL core profile context flags: (none)
    OpenGL core profile profile mask: core profile
    OpenGL core profile extensions:
    OpenGL version string: 3.1 Mesa 20.0.8
    OpenGL shading language version string: 1.40
    OpenGL context flags: (none)
    OpenGL extensions:
    OpenGL ES profile version string: OpenGL ES 3.1 Mesa 20.0.8
    OpenGL ES profile shading language version string: OpenGL ES GLSL ES 3.10
    OpenGL ES profile extensions:
    ```

## Inference Hand

1. Log in to the VM by remote desktop
1. Open a terminal
1. Find the exact path to build for GPU and CPU
    * https://google.github.io/mediapipe/solutions/hands.html#desktop
1. Inference on GPU
    1. Build
        * ```bazel build --copt -DMESA_EGL_NO_X11_HEADERS --copt -DEGL_NO_X11 mediapipe/examples/desktop/hand_tracking:hand_tracking_gpu```
    1. Inference

        ```bash
        export GLOG_logtostderr=1 
        bazel-bin/mediapipe/examples/desktop/hand_tracking/hand_tracking_gpu \
        --input_video_path=<test_video_file> \
        --output_video_path=output_gpu.mp4 \
        --calculator_graph_config_file=mediapipe/graphs/hand_tracking/hand_tracking_desktop_live_gpu.pbtxt
        ```

    1. Output

        ```bash
        ...
        I20210521 03:22:22.308243  6065 demo_run_graph_main_gpu.cc:58] Initialize the calculator graph.
        I20210521 03:22:22.320436  6065 demo_run_graph_main_gpu.cc:62] Initialize the GPU.
        I20210521 03:22:22.336211  6065 gl_context_egl.cc:163] Successfully initialized EGL. Major : 1 Minor: 5
        I20210521 03:22:22.419139  6072 gl_context.cc:331] GL version: 3.2 (OpenGL ES 3.2 NVIDIA 465.19.01)
        I20210521 03:22:22.419373  6065 demo_run_graph_main_gpu.cc:68] Initialize the camera or load the video.
        I20210521 03:22:22.439425  6065 demo_run_graph_main_gpu.cc:89] Start running the calculator graph.
        I20210521 03:22:22.445173  6065 demo_run_graph_main_gpu.cc:94] Start grabbing and processing frames.
        INFO: Initialized TensorFlow Lite runtime.
        INFO: Created TensorFlow Lite delegate for GPU.
        INFO: Replacing 119 node(s) with delegate (TfLiteGpuDelegate) node, yielding 1 partitions.
        INFO: Replacing 64 node(s) with delegate (TfLiteGpuDelegate) node, yielding 1 partitions.
        I20210521 03:22:22.983477  6065 demo_run_graph_main_gpu.cc:171] Prepare video writer.
        I20210521 03:22:25.469522  6065 demo_run_graph_main_gpu.cc:105] Empty frame, end of video reached.
        I20210521 03:22:25.469570  6065 demo_run_graph_main_gpu.cc:186] Shutting down.
        I20210521 03:22:26.507759  6065 demo_run_graph_main_gpu.cc:200] Success!
        ```

1. Inference on CPU
    1. Build
        * ```bazel build --copt -DMESA_EGL_NO_X11_HEADERS --copt -DEGL_NO_X11 mediapipe/examples/desktop/hand_tracking:hand_tracking_cpu```
    1. Inference

        ```bash
        export GLOG_logtostderr=1 
        bazel-bin/mediapipe/examples/desktop/hand_tracking/hand_tracking_cpu \
        --input_video_path=<test_video_file> \
        --output_video_path=output_cpu.mp4 \
        --calculator_graph_config_file=mediapipe/graphs/hand_tracking/hand_tracking_desktop_live.pbtxt
        ```

    1. Output

        ```bash
        ...
        I20210521 03:33:19.245232  6143 demo_run_graph_main.cc:54] Initialize the calculator graph.
        I20210521 03:33:19.257170  6143 demo_run_graph_main.cc:58] Initialize the camera or load the video.
        I20210521 03:33:19.279414  6143 demo_run_graph_main.cc:79] Start running the calculator graph.
        I20210521 03:33:19.294287  6143 gl_context_egl.cc:163] Successfully initialized EGL. Major : 1 Minor: 5
        I20210521 03:33:19.359954  6156 gl_context.cc:331] GL version: 3.2 (OpenGL ES 3.2 NVIDIA 465.19.01)
        I20210521 03:33:19.365622  6143 demo_run_graph_main.cc:84] Start grabbing and processing frames.
        INFO: Initialized TensorFlow Lite runtime.
        INFO: Created TensorFlow Lite XNNPACK delegate for CPU.
        INFO: Replacing 117 node(s) with delegate (TfLiteXNNPackDelegate) node, yielding 2 partitions.
        INFO: Replacing 64 node(s) with delegate (TfLiteXNNPackDelegate) node, yielding 1 partitions.
        I20210521 03:33:19.599210  6143 demo_run_graph_main.cc:128] Prepare video writer.
        I20210521 03:33:28.356304  6143 demo_run_graph_main.cc:95] Empty frame, end of video reached.
        I20210521 03:33:28.356343  6143 demo_run_graph_main.cc:143] Shutting down.
        I20210521 03:33:29.403970  6143 demo_run_graph_main.cc:157] Success!
        ```

## Inference Pose

1. Log in to the VM by remote desktop
1. Open a terminal
1. Find the exact path to build for GPU and CPU
    * https://google.github.io/mediapipe/solutions/pose.html#desktop
1. Inference on GPU
    1. Build
        * ```bazel build --copt -DMESA_EGL_NO_X11_HEADERS --copt -DEGL_NO_X11 mediapipe/examples/desktop/pose_tracking:pose_tracking_gpu```
    1. Inference

        ```bash
        export GLOG_logtostderr=1 
        bazel-bin/mediapipe/examples/desktop/pose_tracking/pose_tracking_gpu \
        --calculator_graph_config_file=mediapipe/graphs/pose_tracking/pose_tracking_gpu.pbtxt \
        --input_video_path=<test_video_file> \
        --output_video_path=output_pose_gpu.mp4 
        ```

    1. Output

        ```bash
        ...
        node {
        calculator: "PoseRendererGpu"
        input_stream: "IMAGE:throttled_input_video"
        input_stream: "LANDMARKS:pose_landmarks"
        input_stream: "ROI:roi_from_landmarks"
        input_stream: "DETECTION:pose_detection"
        output_stream: "IMAGE:output_video"
        }
        I20210521 03:49:45.011806  6774 demo_run_graph_main_gpu.cc:58] Initialize the calculator graph.
        I20210521 03:49:45.029114  6774 demo_run_graph_main_gpu.cc:62] Initialize the GPU.
        I20210521 03:49:45.044044  6774 gl_context_egl.cc:163] Successfully initialized EGL. Major : 1 Minor: 5
        I20210521 03:49:45.108691  6781 gl_context.cc:331] GL version: 3.2 (OpenGL ES 3.2 NVIDIA 465.19.01)
        I20210521 03:49:45.108922  6774 demo_run_graph_main_gpu.cc:68] Initialize the camera or load the video.
        I20210521 03:49:45.129173  6774 demo_run_graph_main_gpu.cc:89] Start running the calculator graph.
        I20210521 03:49:45.135236  6774 demo_run_graph_main_gpu.cc:94] Start grabbing and processing frames.
        INFO: Initialized TensorFlow Lite runtime.
        INFO: Created TensorFlow Lite delegate for GPU.
        INFO: Replacing 229 node(s) with delegate (TfLiteGpuDelegate) node, yielding 1 partitions.
        INFO: Replacing 318 node(s) with delegate (TfLiteGpuDelegate) node, yielding 1 partitions.
        I20210521 03:49:46.838106  6774 demo_run_graph_main_gpu.cc:171] Prepare video writer.
        I20210521 03:49:49.140852  6774 demo_run_graph_main_gpu.cc:105] Empty frame, end of video reached.
        I20210521 03:49:49.140890  6774 demo_run_graph_main_gpu.cc:186] Shutting down.
        I20210521 03:49:50.159099  6774 demo_run_graph_main_gpu.cc:200] Success!
        ```

1. Inference on CPU
    1. Build
        * ```bazel build --copt -DMESA_EGL_NO_X11_HEADERS --copt -DEGL_NO_X11 mediapipe/examples/desktop/pose_tracking:pose_tracking_cpu```
    1. Inference

        ```bash
        export GLOG_logtostderr=1 
        bazel-bin/mediapipe/examples/desktop/pose_tracking/pose_tracking_cpu \
        --input_video_path=Adjectives-1.loud-MVI_9448.MOV \
        --output_video_path=output_pose_cpu.mp4  \
        --calculator_graph_config_file=mediapipe/graphs/pose_tracking/pose_tracking_cpu.pbtxt
        ```

    1. Output

        ```bash
        ...
        node {
        calculator: "PoseRendererCpu"
        input_stream: "IMAGE:throttled_input_video"
        input_stream: "LANDMARKS:pose_landmarks"
        input_stream: "ROI:roi_from_landmarks"
        input_stream: "DETECTION:pose_detection"
        output_stream: "IMAGE:output_video"
        }
        I20210521 03:52:32.027629  6815 demo_run_graph_main.cc:54] Initialize the calculator graph.
        I20210521 03:52:32.044478  6815 demo_run_graph_main.cc:58] Initialize the camera or load the video.
        I20210521 03:52:32.065657  6815 demo_run_graph_main.cc:79] Start running the calculator graph.
        I20210521 03:52:32.085031  6815 gl_context_egl.cc:163] Successfully initialized EGL. Major : 1 Minor: 5
        I20210521 03:52:32.159987  6828 gl_context.cc:331] GL version: 3.2 (OpenGL ES 3.2 NVIDIA 465.19.01)
        I20210521 03:52:32.166441  6815 demo_run_graph_main.cc:84] Start grabbing and processing frames.
        INFO: Initialized TensorFlow Lite runtime.
        INFO: Created TensorFlow Lite XNNPACK delegate for CPU.
        INFO: Replacing 223 node(s) with delegate (TfLiteXNNPackDelegate) node, yielding 6 partitions.
        INFO: Replacing 318 node(s) with delegate (TfLiteXNNPackDelegate) node, yielding 1 partitions.
        I20210521 03:52:33.084416  6815 demo_run_graph_main.cc:128] Prepare video writer.
        I20210521 03:52:46.590143  6815 demo_run_graph_main.cc:95] Empty frame, end of video reached.
        I20210521 03:52:46.590178  6815 demo_run_graph_main.cc:143] Shutting down.
        I20210521 03:52:47.616358  6815 demo_run_graph_main.cc:157] Success!
        ```

## Benchmark GPU and CPU

Use `time` command to benchmark on Standard NC6s_v3 from reading a .mov file and output a .mp4 file.
Benchmark file: Adjectives-1.loud-MVI_9448.MOV from [here](https://zenodo.org/record/4010759#.YKds1Y3iuUl).

### Hand

|      | CPU     | GPU     |
|------|---------|---------|
| real | 10.188s | 4.251s  |
| user | 17.238s | 10.592s |
| sys  | 1.100s  | 1.298s  |

### Pose

|      | CPU     | GPU     |
|------|---------|---------|
| real | 15.491s | 5.317s  |
| user | 22.778  | 11.467s |
| sys  | 0.624s  | 1.22s   |

## TroubleShooting

* Build

  * #include <EGL/egl.h> no such file or directory \
    Answer: `sudo apt-get install mesa-common-dev libegl1-mesa-dev libgles2-mesa-dev`

  * #include_next <stdlib.h> no such file or directory \
    Answer: `cd mediapipe` then `sh setup_opencv.sh`

* Inference

  * Failed to run the graph: ; eglChooseConfig() returned no matching EGL configuration for RGBA8888 D16 ES2 request. \
    Answer: There are two options here.
    1. Edit `mediapipe/mediapipe/gpu/gl_context_egl.cc`
        * ```vim mediapipe/gpu/gl_context_egl.cc```
        * find this line, `EGL_PBUFFER_BIT | EGL_WINDOW_BIT`
        * Change to `EGL_PBUFFER_BIT`
        * Build and inference again
        You may find some errors after inferencing but the output file is okay.
    1. Install GUI on ubuntu 18.04
        * Please check this [site](https://docs.microsoft.com/en-us/azure/virtual-machines/linux/use-remote-desktop).
        * Then inference on the terminal of rdp
* Others

  * Why didn't `nvidia-smi` show anything while doing inference with GPU? \
      Answer: It might because nvidia driver cannot recognize the code. Please check [here](https://github.com/google/mediapipe/issues/1936).

## To improve

* Extract landmarks from the output video
* Maybe we can change some code to build mediapipe with GPU support
* Use docker?