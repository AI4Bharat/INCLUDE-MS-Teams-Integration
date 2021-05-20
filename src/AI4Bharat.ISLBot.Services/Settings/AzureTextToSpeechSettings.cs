// <copyright file="AzureSettings.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// </copyright>

namespace AI4Bharat.ISLBot.Service.Settings
{
    public class AzureTextToSpeechSettings
    {
        /// <summary>
        /// Gets or sets the speech service subscription key
        /// </summary>
        /// <value>The speech service subscription key</value>
        public string SpeechSubscriptionKey { get; set; }

        /// <summary>
        /// Gets or sets the speech service region
        /// </summary>
        /// <value>The speech service region</value>
        public string SpeechRegion { get; set; }

        /// <summary>
        /// Gets or sets the speech voice name
        /// </summary>
        /// <value>The speech voice name</value>
        public string SpeechSynthesisVoiceName { get; set; }
    }

}
