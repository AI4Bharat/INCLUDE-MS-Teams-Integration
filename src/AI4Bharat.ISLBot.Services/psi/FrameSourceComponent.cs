// <copyright file="FrameSourceComponent.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using Microsoft.Graph.Communications.Common.Telemetry;
using Microsoft.Psi;
using Microsoft.Psi.Components;
using Microsoft.Psi.Imaging;
using Microsoft.Skype.Bots.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AI4Bharat.ISLBot.Services.psi
{
    public class FrameSourceComponent : ISourceComponent
    {
        private readonly Pipeline pipeline;
        private readonly IGraphLogger logger;
        private bool started;

        public Emitter<Dictionary<string, Shared<Image>>> Video { get; }

        public FrameSourceComponent(Pipeline pipeline, IGraphLogger logger)
        {
            this.pipeline = pipeline;
            this.logger = logger;
            this.Video = pipeline.CreateEmitter<Dictionary<string, Shared<Image>>>(this, "Teams Video");
        }

        public void Start(Action<DateTime> notifyCompletionTime)
        {
            notifyCompletionTime(DateTime.MaxValue);
            this.started = true;
        }

        public void Stop(DateTime finalOriginatingTime, Action notifyCompleted)
        {
            this.started = false;
            notifyCompleted();
        }

        public void ReceiveFrame(VideoMediaBuffer videoFrame, string participantId)
        {

            var originatingTime = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(videoFrame.Timestamp);
            var frames = new Dictionary<string, Shared<Image>>();

            using (var sharedImage = ImagePool.GetOrCreate(
                videoFrame.VideoFormat.Width,
                videoFrame.VideoFormat.Height,
                PixelFormat.BGR_24bpp))
            {
                var timestamp = (long)videoFrame.Timestamp;
                if (timestamp == 0)
                {
                    this.logger.Warn($"Original sender timestamp is zero: {participantId}");
                    return;
                }
                var length = videoFrame.VideoFormat.Width * videoFrame.VideoFormat.Height * 12 / 8; // This is how to calculate NV12 buffer size
                if (length > videoFrame.Length)
                {
                    return;
                }
                byte[] data = new byte[length];

                try
                {
                    Marshal.Copy(videoFrame.Data, data, 0, length);
                    var bgrImage = NV12toBGR(data, videoFrame.VideoFormat.Width, videoFrame.VideoFormat.Height);
                    sharedImage.Resource.CopyFrom(bgrImage);
                }
                catch (Exception ex)
                {
                    this.logger.Warn($"ON FAILURE: length: {videoFrame.Length}, height: {videoFrame.VideoFormat.Height}, width: {videoFrame.VideoFormat.Width}");
                    this.logger.Error(ex);
                    return;
                }
                lock (this.Video)
                {
                    if (originatingTime > this.Video.LastEnvelope.OriginatingTime)
                    {
                        frames.Add(participantId, sharedImage);
                        this.Video.Post(frames, originatingTime);
                    }
                    else
                    {
                        this.logger.Warn("Out of order frame");
                    }
                }
            }

        }

        public static byte[] NV12toBGR(byte[] data, int width, int height)
        {
            var destWidth = width;
            var destHeight = height;

            int resultStride = 4 * (((3 * destWidth) + 3) / 4);
            byte[] result = new byte[destHeight * resultStride];
            int uvStart = width * height;
            int uvStride = width;

            for (var y = 0; y < height; y++)
            {
                var pos = y * width;
                for (var x = 0; x < width; x++)
                {
                    var vIndex = uvStart + ((y >> 1) * width) + (x & ~1);

                    //// https://msdn.microsoft.com/en-us/library/windows/desktop/dd206750(v=vs.85).aspx
                    //// https://en.wikipedia.org/wiki/YUV
                    var c = data[pos] - 16;
                    var d = data[vIndex] - 128;
                    var e = data[vIndex + 1] - 128;
                    c = c < 0 ? 0 : c;

                    var r = ((298 * c) + (409 * e) + 128) >> 8;
                    var g = ((298 * c) - (100 * d) - (208 * e) + 128) >> 8;
                    var b = ((298 * c) + (516 * d) + 128) >> 8;
                    var rByte = (byte)Math.Max(0, Math.Min(255, r));
                    var gByte = (byte)Math.Max(0, Math.Min(255, g));
                    var bByte = (byte)Math.Max(0, Math.Min(255, b));

                    result[(y * resultStride) + (3 * x)] = bByte;
                    result[(y * resultStride) + (3 * x) + 1] = gByte;
                    result[(y * resultStride) + (3 * x) + 2] = rByte;

                    pos++;
                }
            }

            return result;
        }
    }
}
