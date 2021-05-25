// <copyright file="SendAudioToBotComponent.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using AI4Bharat.ISLBot.Services.Settings;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Graph.Communications.Common.Telemetry;
using Microsoft.Psi;
using Microsoft.Psi.Components;
using Microsoft.Skype.Bots.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AI4Bharat.ISLBot.Services.CognitiveServices
{
    public class TextToSpeechComponent : AsyncConsumerProducer<string, byte[]>
    {
        private readonly SpeechSynthesizer synthesizer;
        private readonly IGraphLogger logger;

        public TextToSpeechComponent(Pipeline pipeline, AzureTextToSpeechSettings settings, IGraphLogger logger) : base(pipeline)
        {
            var speechConfig = SpeechConfig.FromSubscription(settings.SpeechSubscriptionKey, settings.SpeechRegion);
            speechConfig.SpeechSynthesisVoiceName = settings.SpeechSynthesisVoiceName;
            this.synthesizer = new SpeechSynthesizer(speechConfig, null);
            this.logger = logger;
        }

        protected override async Task ReceiveAsync(string word, Envelope envelope)
        {
            try
            {
                var audio = await CreateSpeechByteArray(word);
                this.Out.Post(audio, envelope.OriginatingTime);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex);
            }
        }

        private async Task<byte[]> CreateSpeechByteArray(string word)
        {
            var result = await synthesizer.SpeakTextAsync(word);
            return result.AudioData;
        }
    }
}
