// <copyright file="SpeechHelper.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Graph;
using Microsoft.Graph.Communications.Common.Transport;
using Microsoft.Skype.Bots.Media;
using Microsoft.WindowsAzure.Storage;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AI4Bharat.ISLBot.Services.Bot
{
    public class SpeechHelper
    {

        private BlobClient GetBlobClient(string blobConnectionString, string blobContainerName, string fileName)
        {
            var blobServiceClient = new BlobServiceClient(blobConnectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(blobContainerName);
            BlobClient blobClient = containerClient.GetBlobClient(fileName);

            return blobClient;
        }

        private BlobSasBuilder GetBlobSasBuilder(BlobClient blobClient)
        {
            // Create a SAS token that's valid for one hour.
            BlobSasBuilder sasBuilder = new BlobSasBuilder()
            {
                BlobContainerName = blobClient.GetParentBlobContainerClient().Name,
                BlobName = blobClient.Name,
                Resource = "b",
            };
            sasBuilder.ExpiresOn = DateTimeOffset.UtcNow.AddHours(1);
            sasBuilder.SetPermissions(BlobSasPermissions.Read |
                BlobSasPermissions.Write);

            return sasBuilder;
        }

        /// <summary>
        /// Get speech file absolute uri with SAS token.
        /// </summary>
        /// <param name="blobConnectionString">connection string</param>
        /// <param name="blobContainerName">container name</param>
        /// <param name="fileName">name of the speech file.</param>
        /// <returns>Absolute URI</returns>
        private string GetBlobSas(string blobConnectionString, string blobContainerName, string fileName)
        {

            var blobSasBuilder = this.GetBlobSasBuilder(this.GetBlobClient(blobConnectionString, blobContainerName, fileName));

            // Builds an instance of StorageSharedKeyCredential
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(blobConnectionString);
            var storageSharedKeyCredential = new StorageSharedKeyCredential(storageAccount.Credentials.AccountName, storageAccount.Credentials.ExportBase64EncodedKey());

            // Builds the Sas URI.
            BlobSasQueryParameters sasQueryParameters = blobSasBuilder.ToSasQueryParameters(storageSharedKeyCredential);

            UriBuilder fullUri = new UriBuilder()
            {
                Scheme = "https",
                Host = string.Format("{0}.blob.core.windows.net", storageAccount.Credentials.AccountName),
                Path = string.Format("{0}/{1}", blobContainerName, fileName),
                Query = sasQueryParameters.ToString(),
            };


            return fullUri.ToString();
        }

        /// <summary>
        /// create blob from byte array
        /// </summary>
        /// <param name="connectionString">blob connection string</param>
        /// <param name="containerName">blob container</param>
        /// <param name="fileName">filename to save</param>
        /// <param name="buffer">byte array</param>
        /// <returns>Url</returns>
        private async Task<string> CreateBlobFromByteArrayAsync(string connectionString, string containerName, string fileName, byte[] buffer)
        {
            using (var stream = new MemoryStream(buffer, writable: false))
            {
                return await this.CreateBlobFromStreamAsync(connectionString, containerName, fileName, stream);
            }
        }

        /// <summary>
        /// Create blob from stream
        /// </summary>
        /// <param name="connectionString">blob connecttion string</param>
        /// <param name="containerName">container name</param>
        /// <param name="fileName">filename to save</param>
        /// <param name="stream">memory stream</param>
        /// <returns>Url</returns>
        private async Task<string> CreateBlobFromStreamAsync(string connectionString, string containerName, string fileName, Stream stream)
        {
            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(fileName);

            await blobClient.UploadAsync(stream, true);
            return blobClient.Name;
        }

        public async Task createSpeechWavFile(string subscriptionKey, string serviceRegion, string word)
        {
            var speechConfig = SpeechConfig.FromSubscription(subscriptionKey, serviceRegion);

            speechConfig.SpeechSynthesisVoiceName = "en-US-GuyRUS";

            var synthesizer = new SpeechSynthesizer(speechConfig, null as AudioConfig);
            var result = await synthesizer.SpeakTextAsync(word);

            var audioDataStream = AudioDataStream.FromResult(result);
            await audioDataStream.SaveToWaveFileAsync($"{word}.wav");
        }

        public async Task<AudioDataStream> createSpeechStream(string subscriptionKey, string serviceRegion, string word)
        {
            var speechConfig = SpeechConfig.FromSubscription(subscriptionKey, serviceRegion);

            speechConfig.SpeechSynthesisVoiceName = "en-US-GuyRUS";

            var synthesizer = new SpeechSynthesizer(speechConfig, null as AudioConfig);
            var result = await synthesizer.SpeakTextAsync(word);

            return AudioDataStream.FromResult(result);
        }


        public async Task<Byte[]> CreateSpeechByteArray(string subscriptionKey, string serviceRegion, string word)
        {
            var speechConfig = SpeechConfig.FromSubscription(subscriptionKey, serviceRegion);

            speechConfig.SpeechSynthesisVoiceName = "en-US-GuyRUS";

            var synthesizer = new SpeechSynthesizer(speechConfig, null as AudioConfig);
            var result = await synthesizer.SpeakTextAsync(word);

            return result.AudioData;
        }

        public async Task<string> createSpeechWavBlob(string subscriptionKey,string serviceRegion,  string word, string blobConnectionString, string blobContainerName)
        {
            var speechConfig = SpeechConfig.FromSubscription(subscriptionKey, serviceRegion);

            speechConfig.SpeechSynthesisVoiceName = "en-US-GuyRUS";

            var synthesizer = new SpeechSynthesizer(speechConfig, null as AudioConfig);
            var result = await synthesizer.SpeakTextAsync(word);

            return await CreateBlobFromByteArrayAsync(blobConnectionString, blobContainerName, $"{word}​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​​.wav", result.AudioData);
        }


        /// <summary>
        /// send audio file to participant.
        /// </summary>
        /// <param name="callId">Call identity.</param>
        /// <param name="appSettings">application settings.</param>
        /// <param name="fileName">name of the audio file to be played from blob.</param>
        /// <returns>Task.</returns>
        public async Task SendAudioAsync(string callId, string fileName, GraphServiceClient graphServiceClient, IGraphClient graphApiClient, string blobConnectionString, string blobContainerName, string tenanatId)
        {
            var prompts = new Prompt[]
            {
                new MediaPrompt
                {
                    MediaInfo = new MediaInfo()
                    { 
                         // this is from Sas but we can just use the file name we saved somewhere
                         Uri = GetBlobSas(blobConnectionString, blobContainerName, fileName),
                         ResourceId = fileName,
                    },
                },
            };

            var playPromptRequest = graphServiceClient.Communications.Calls[callId].PlayPrompt(
                prompts: prompts,
                clientContext: callId).Request();

            // todo determine scenario identifier
            var scenarioId = Guid.NewGuid();
            await graphApiClient.SendAsync<PlayPromptOperation>(playPromptRequest, RequestType.Create, tenanatId, scenarioId).ConfigureAwait(false);

            // todo: exception handling​​​​​​​​​​​​​​​
        }

        // example from
        // https://github.com/microsoft/BotBuilder-RealTimeMediaCalling/blob/1212fc005e0611ff543f930d30b587e76339bfc0/Samples/AudioVideoPlayerBot/FrontEnd/Utilities.cs

        public AudioSendBuffer CreateAudioMediaBuffer(long referenceTime, Byte[] audio)
        {
            IntPtr unmanagedBuffer = Marshal.AllocHGlobal(audio.Length);
            Marshal.Copy(audio, 0, unmanagedBuffer, audio.Length);

            return new AudioSendBuffer(unmanagedBuffer, audio.Length, AudioFormat.Pcm16K, referenceTime);
        }
    }
}