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

namespace AI4Bharat.ISLBot.Services.psi
{
    public class ISLPipeline: IDisposable
    {
        private readonly IGraphLogger logger;
        private Pipeline pipeline;
        private FrameSourceComponent frameSourceComponent;

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
            mpegConfig.ImageWidth = 480;
            mpegConfig.ImageHeight = 270;
            mpegConfig.PixelFormat = PixelFormat.BGR_24bpp;

            var resized = frameSourceComponent
                .Video
                .Select(v => v.First().Value)
                .Resize(480, 270);

            var writer = new Mpeg4Writer(pipeline, "output.mp4", mpegConfig);
            resized.PipeTo(writer.ImageIn);

            var store = PsiStore.Create(pipeline, "demo", "C:\\psistore");
            resized.Write("video", store);

            this.pipeline.PipelineExceptionNotHandled += (_, ex) =>
            {
                this.logger.Error($"PSI PIPELINE ERROR: {ex.Exception.Message}");
            };

            pipeline.RunAsync();
            Task.Run(async () => {
                await Task.Delay(30000);
                this.logger.Warn("STOPPPPPPPPPIIIIIIIIINNNNNNNNGGGGGGGGGGGGGGGGG");
                pipeline.Dispose();
            });

            //this.pipeline.RunAsync();
        }

        public void OnVideoMediaReceived(VideoMediaBuffer videoFrame, string participantId)
        {
            frameSourceComponent.ReceiveFrame(videoFrame, participantId);
        }
    }
}
