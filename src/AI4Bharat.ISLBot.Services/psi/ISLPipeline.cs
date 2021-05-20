// <copyright file="ISLPipeline.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using Microsoft.Graph.Communications.Common.Telemetry;
using Microsoft.Psi;

using Microsoft.Psi.Components;
using Microsoft.Psi.Imaging;
using Microsoft.Psi.Media;
using Microsoft.Skype.Bots.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AI4Bharat.ISLBot.Services.Psi
{
    public class ISLPipeline: IDisposable
    {
        private readonly IGraphLogger logger;
        private Pipeline pipeline;
        private FrameSourceComponent frameSourceComponent;

        private const int IMAGE_WIDTH = 480;
        private const int IMAGE_HEIGHT = 270;

        public ISLPipeline(IGraphLogger logger)
        {
            this.logger = logger;
        }

        public void Dispose()
        {
            if (this.pipeline != null)
            {
                this.pipeline.Dispose();
                this.pipeline = null;
            }
        }

        public void DoWork()
        {
            this.pipeline = Pipeline.Create("Teams Pipeline");

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
                .Do(label => this.logger.Warn(label));

            var store = PsiStore.Create(pipeline, "Bot", @"E:\psi");
            resized.Write("video", store);
            labelStream.Write("label", store);

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
    }
}
