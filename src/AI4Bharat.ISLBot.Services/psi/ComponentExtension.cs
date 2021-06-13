// <copyright file="WriteMP4Component.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using AI4Bharat.ISLBot.Services.Settings;
using AI4Bharat.ISLBot.Services.CognitiveServices;
using Microsoft.Graph.Communications.Common.Telemetry;
using Microsoft.Psi;
using Microsoft.Psi.Imaging;
using Microsoft.Psi.Media;
using Microsoft.Skype.Bots.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AI4Bharat.ISLBot.Services.Psi
{
    public static class ComponentExtension
    {
        public static IProducer<string> WriteMP4InBatches(this IProducer<Shared<Image>> source, TimeSpan batchInterval, string basePath, Mpeg4WriterConfiguration mpegConfig)
        {
            var fileNames = Generators.Sequence(
                    source.Out.Pipeline,
                    $@"{basePath}\{DateTime.UtcNow.Ticks}.mp4",
                    _ => $@"{basePath}\{DateTime.UtcNow.Ticks}.mp4",
                    batchInterval
                );

            var lastOriginatingTimes = new Dictionary<string, DateTime>();
            var mp4Writers = new Dictionary<string, Mpeg4Writer>();

            var parallel = source.Pair(fileNames).Parallel(
                tup => new Dictionary<string, Shared<Image>>() { { tup.Item2, tup.Item1 } },
                (f, s) =>
                {
                    mp4Writers.Add(f, new Mpeg4Writer(s.Out.Pipeline, f, mpegConfig));
                    s.PipeTo(mp4Writers[f]);
                    return Generators.Once(s.Out.Pipeline, f);
                },
                f =>
                {
                    if (lastOriginatingTimes.Count >= 2)
                    {
                        return lastOriginatingTimes.Keys.Skip(lastOriginatingTimes.Count() - 2).First();
                    }
                    return lastOriginatingTimes.FirstOrDefault().Key;
                },
                false,
                null,
                null,
                (key, message, originatingTime) =>
                {
                    if (message.ContainsKey(key))
                    {
                        lastOriginatingTimes[key] = originatingTime;
                        return (false, DateTime.MaxValue);
                    }
                    mp4Writers[key].Dispose();
                    mp4Writers.Remove(key);
                    return (true, lastOriginatingTimes[key]);
                });

            return parallel.Out;
        }
        
        public static IProducer<(string filename, string label)> CallModel(this IProducer<string> source, string endpointUrl, string basePath, IGraphLogger logger)
        {
            var callApi = new CallModelComponent(source.Out.Pipeline, endpointUrl, basePath, logger);
            source.PipeTo(callApi, DeliveryPolicy.LatestMessage);
            return callApi.Out;
        }

        public static IProducer<byte[]> PerformTextToSpeech(this IProducer<string> source, AzureTextToSpeechSettings settings, IGraphLogger logger)
        {
            var comp = new TextToSpeechComponent(source.Out.Pipeline, settings, logger);
            source.PipeTo(comp);
            return comp.Out;
        }
    }
}
