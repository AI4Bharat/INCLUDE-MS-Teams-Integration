// <copyright file="ISLPipeline.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using AI4Bharat.ISLBot.Service.Settings;
using AI4Bharat.ISLBot.Services.CognitiveServices;
using Microsoft.Graph.Communications.Common.Telemetry;
using Microsoft.Psi;

using Microsoft.Psi.Components;
using Microsoft.Psi.Imaging;
using Microsoft.Psi.Media;
using Microsoft.Skype.Bots.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AI4Bharat.ISLBot.Services.Psi
{
    public class ISLPipeline: IDisposable
    {
        private readonly IGraphLogger logger;
        private readonly AzureTextToSpeechSettings ttsSettings;
        private readonly Action<List<AudioMediaBuffer>> sendToBot;
        private Pipeline pipeline;
        private FrameSourceComponent frameSourceComponent;

        private const int IMAGE_WIDTH = 480;
        private const int IMAGE_HEIGHT = 270;

        public ISLPipeline(IGraphLogger logger, AzureTextToSpeechSettings ttsSettings, Action<List<AudioMediaBuffer>> sendToBot)
        {
            this.logger = logger;
            this.ttsSettings = ttsSettings;
            this.sendToBot = sendToBot;
        }

        public void Dispose()
        {
            if (this.pipeline != null)
            {
                this.pipeline.Dispose();
                this.pipeline = null;
            }
        }

        public void CreateAndStartPipeline()
        {
            this.pipeline = Pipeline.Create("Teams Pipeline", enableDiagnostics: true);

            this.frameSourceComponent = new FrameSourceComponent(this.pipeline, logger);

            var mpegConfig = Mpeg4WriterConfiguration.Default;
            mpegConfig.ContainsAudio = false;
            mpegConfig.ImageWidth = IMAGE_WIDTH;
            mpegConfig.ImageHeight = IMAGE_HEIGHT;
            mpegConfig.PixelFormat = PixelFormat.BGR_24bpp;

            var basePath = @"E:\Recording";
            var endpointUrl = @"http://vm7islbotdemo.centralindia.cloudapp.azure.com:8000/inference";

            var resized = frameSourceComponent
                .Video
                .Select(v => v.First().Value)
                .Resize(IMAGE_WIDTH, IMAGE_HEIGHT);
            var labelStream = resized
                .WriteMP4InBatches(TimeSpan.FromSeconds(5), basePath, mpegConfig)
                .CallModel(endpointUrl, basePath, logger);
            labelStream
                .PerformTextToSpeech(this.ttsSettings, this.logger)
                .Do(bytes => sendToBot(CreateAudioMediaBuffers(DateTime.UtcNow.Ticks, bytes)));

            labelStream
                .Do(label => this.logger.Warn(label));

            var store = PsiStore.Create(pipeline, "Bot", @"C:\psistore");
            resized.Write("video", store);
            labelStream.Write("label", store);
            pipeline.Diagnostics.Write("Diagnostics", store);

            this.pipeline.PipelineExceptionNotHandled += (_, ex) =>
            {
                this.logger.Error(ex.Exception, $"PSI PIPELINE ERROR: {ex.Exception.Message}");
            };

            pipeline.RunAsync();
            //Task.Run(async () => {
            //    await Task.Delay(30000);
            //    this.logger.Warn("STOPPPPPPPPPIIIIIIIIINNNNNNNNGGGGGGGGGGGGGGGGG");
            //    pipeline.Dispose();
            //});
        }

        public void OnVideoMediaReceived(VideoMediaBuffer videoFrame, string participantId)
        {
            frameSourceComponent.ReceiveFrame(videoFrame, participantId);
        }

        public List<AudioMediaBuffer> CreateAudioMediaBuffers(long currentTick, Byte[] audio)
        {
            var stream = new MemoryStream(audio);

            var audioMediaBuffers = new List<AudioMediaBuffer>();
            var referenceTime = currentTick;

            using (stream)
            {
                byte[] bytesToRead = new byte[640];
                stream.Seek(44, SeekOrigin.Begin);
                while (stream.Read(bytesToRead, 0, bytesToRead.Length) >= 640) //20ms
                {
                    IntPtr unmanagedBuffer = Marshal.AllocHGlobal(640);
                    Marshal.Copy(bytesToRead, 0, unmanagedBuffer, 640);
                    referenceTime += 20 * 10000;
                    var audioBuffer = new AudioSendBuffer(unmanagedBuffer, 640, AudioFormat.Pcm16K,
                        referenceTime);
                    audioMediaBuffers.Add(audioBuffer);
                }
            }
            return audioMediaBuffers;
        }
    }
}
