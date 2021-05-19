// <copyright file="ChannelSourceComponent.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using Microsoft.Psi;
using Microsoft.Psi.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace AI4Bharat.ISLBot.Services.psi
{
    public class ChannelSourceComponent<T> : ISourceComponent
    {
        private readonly Pipeline pipeline;
        private readonly ChannelReader<T> channelReader;
        private readonly Func<T, DateTime> calculateOriginatingTime;
        private Action<DateTime> notifyCompletionTime;

        public Emitter<T> Out { get; }

        private CancellationTokenSource cancellationSource;

        public ChannelSourceComponent(Pipeline pipeline, ChannelReader<T> channelReader, Func<T, DateTime> calculateOriginatingTime)
        {
            this.pipeline = pipeline;
            this.channelReader = channelReader;
            this.calculateOriginatingTime = calculateOriginatingTime;
            this.Out = pipeline.CreateEmitter<T>(this, "Channel Reader");
            this.cancellationSource = new CancellationTokenSource();
            Task.Run(() => this.ReadChannel(), cancellationSource.Token);
        }

        public void Start(Action<DateTime> notifyCompletionTime)
        {
            this.notifyCompletionTime = notifyCompletionTime;
        }

        public void Stop(DateTime finalOriginatingTime, Action notifyCompleted)
        {
            notifyCompleted();
        }

        public async Task ReadChannel()
        {
            while (await channelReader.WaitToReadAsync())
                while (channelReader.TryRead(out T item))
                    this.Out.Post(item, this.calculateOriginatingTime(item));

            this.notifyCompletionTime(DateTime.UtcNow);
        }
    }
}
