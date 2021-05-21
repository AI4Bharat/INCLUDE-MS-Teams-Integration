﻿// <copyright file="ISLPipeline.cs" company="Microsoft Corporation">
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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Image = Microsoft.Psi.Imaging.Image;

namespace AI4Bharat.ISLBot.Services.Psi
{
    public class ISLPipeline : IDisposable
    {
        private readonly IGraphLogger logger;
        private readonly AzureTextToSpeechSettings ttsSettings;
        private readonly Action<List<AudioMediaBuffer>> sendAudioToBot;
        private readonly Action<byte[]> sendScreenShareToBot;
        private Pipeline pipeline;
        private FrameSourceComponent frameSourceComponent;

        private const int IMAGE_WIDTH = 480;
        private const int IMAGE_HEIGHT = 270;

        public int ScreenShareWidth = 1920;
        public int ScreenShareHeight = 1080;
        private Font font = new Font(FontFamily.GenericMonospace, 32);

        public ISLPipeline(IGraphLogger logger, AzureTextToSpeechSettings ttsSettings, Action<List<AudioMediaBuffer>> sendAudioToBot, Action<byte[]> sendScreenShareToBot)
        {
            this.logger = logger;
            this.ttsSettings = ttsSettings;
            this.sendAudioToBot = sendAudioToBot;
            this.sendScreenShareToBot = sendScreenShareToBot;
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
                .Resize(IMAGE_WIDTH, IMAGE_HEIGHT)
                .Name("Resized Frames");

            var fileNames = resized
                .WriteMP4InBatches(TimeSpan.FromSeconds(5), basePath, mpegConfig)
                .Name("FileNames");

            var labelStream = fileNames
                .CallModel(endpointUrl, basePath, logger).Name("Model Result")
                .Do(l => this.logger.Warn($"file: {l.filename} label: {l.label}"));

            labelStream.Item2()
                .PerformTextToSpeech(this.ttsSettings, this.logger).Name("Text To Speech")
                .Do(bytes => this.sendAudioToBot(CreateAudioMediaBuffers(DateTime.UtcNow.Ticks, bytes))).Name("Send Audio To Bot");

            Generators
                .Repeat(pipeline, true, TimeSpan.FromSeconds(1.0 / 15)).Name("15fps generation event")
                .Pair(labelStream, DeliveryPolicy.LatestMessage, DeliveryPolicy.LatestMessage)
                .Do(f =>
                {
                    try
                    {
                        var text = f.Item3;
                        using (var sharedImage = ProduceScreenShare(text))
                        {
                            var image = sharedImage.Resource;
                            var nv12 = BGRAtoNV12(image.ImageData, image.Width, image.Height);
                            this.sendScreenShareToBot(nv12);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.logger.Error(ex, "Error while screen sharing");
                    }
                }).Name("Screen Share to bot");

            var store = PsiStore.Create(pipeline, "Bot", @"E:\psistore");
            //resized.Write("video", store);
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

        protected Shared<Image> ProduceScreenShare(string label)
        {
            var sharedImage = ImagePool.GetOrCreate(this.ScreenShareWidth, this.ScreenShareHeight, PixelFormat.BGRA_32bpp);
            Bitmap bitmap = new Bitmap(this.ScreenShareWidth, this.ScreenShareHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics graphics = Graphics.FromImage(bitmap);

            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.Clear(Color.Black);
            graphics.FillRectangle(Brushes.Black, 10, 10, this.ScreenShareWidth - 10, this.ScreenShareHeight - 10);
            var size = graphics.MeasureString(label, this.font);
            graphics.DrawString(label, this.font, Brushes.Green, new PointF((this.ScreenShareWidth / 2) - (size.Width / 2), (this.ScreenShareHeight / 2) - (size.Height / 2)));
            sharedImage.Resource.CopyFrom(bitmap);
            graphics.Dispose();
            return sharedImage;
        }

        /// <summary>
        /// Convert BGRA image to NV12.
        /// </summary>
        /// <param name="data">BGRA data.</param>
        /// <param name="width">Image width.</param>
        /// <param name="height">Image height.</param>
        /// <returns>NV12 encoded bytes.</returns>
        private unsafe byte[] BGRAtoNV12(IntPtr data, int width, int height)
        {
            var bytes = (byte*)data.ToPointer();
            byte[] result = new byte[(int)(1.5 * (width * height))];

            // https://www.fourcc.org/fccyvrgb.php
            for (var i = 0; i < width * height; i++)
            {
                var p = bytes + (i * 4);
                var b = *p;
                var g = *(p + 1);
                var r = *(p + 2);
                var y = (byte)Math.Max(0, Math.Min(255, (0.257 * r) + (0.504 * g) + (0.098 * b) + 16));
                result[i] = y;
            }

            var stride = width * 4;
            var uv = width * height;
            for (var j = 0; j < height; j += 2)
            {
                for (var i = 0; i < width; i += 2)
                {
                    var p = bytes + (i * 4) + (j * width * 4);
                    var b = (*p + *(p + 4) + *(p + stride) + *(p + stride + 4)) / 4;
                    var g = (*(p + 1) + *(p + 5) + *(p + stride + 1) + *(p + stride + 5)) / 4;
                    var r = (*(p + 2) + *(p + 6) + *(p + stride + 2) + *(p + stride + 6)) / 4;
                    var u = (byte)Math.Max(0, Math.Min(255, -(0.148 * r) - (0.291 * g) + (0.439 * b) + 128));
                    var v = (byte)Math.Max(0, Math.Min(255, (0.439 * r) - (0.368 * g) - (0.071 * b) + 128));
                    result[uv++] = u;
                    result[uv++] = v;
                }
            }

            return result;
        }
    }
}