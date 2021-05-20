// <copyright file="BotMediaStream.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using Microsoft.Graph.Communications.Calls;
using Microsoft.Graph.Communications.Calls.Media;
using Microsoft.Graph.Communications.Common;
using Microsoft.Graph.Communications.Common.Telemetry;
using Microsoft.Skype.Bots.Media;
using Microsoft.Skype.Internal.Media.Services.Common;
using System;
using System.Collections.Generic;
using AI4Bharat.ISLBot.Service.Settings;
using System.Linq;
using System.Threading;
using AI4Bharat.ISLBot.Services.Psi;

namespace AI4Bharat.ISLBot.Services.Bot
{
    /// <summary>
    /// Class responsible for streaming audio and video.
    /// </summary>
    public class BotMediaStream : ObjectRootDisposable
    {

        /// <summary>
        /// Contains a map from simple color/width/height combinations to VideoFormat objects.
        /// </summary>
        public static readonly Dictionary<(VideoColorFormat format, int width, int height), VideoFormat> VideoFormatMap = new Dictionary<(VideoColorFormat format, int width, int height), VideoFormat>()
        {
            { (VideoColorFormat.NV12, 1920, 1080), VideoFormat.NV12_1920x1080_15Fps },
            { (VideoColorFormat.NV12, 1280, 720), VideoFormat.NV12_1280x720_15Fps },
            { (VideoColorFormat.NV12, 848, 480), VideoFormat.NV12_848x480_30Fps },
            { (VideoColorFormat.NV12, 640, 360), VideoFormat.NV12_640x360_15Fps },
            { (VideoColorFormat.NV12, 480, 848), VideoFormat.NV12_480x848_30Fps },
            { (VideoColorFormat.NV12, 480, 270), VideoFormat.NV12_480x270_15Fps },
            { (VideoColorFormat.NV12, 424, 240), VideoFormat.NV12_424x240_15Fps },
            { (VideoColorFormat.NV12, 360, 640), VideoFormat.NV12_360x640_15Fps },
            { (VideoColorFormat.NV12, 320, 180), VideoFormat.NV12_320x180_15Fps },
            { (VideoColorFormat.NV12, 270, 480), VideoFormat.NV12_270x480_15Fps },
            { (VideoColorFormat.NV12, 240, 424), VideoFormat.NV12_240x424_15Fps },
            { (VideoColorFormat.NV12, 180, 320), VideoFormat.NV12_180x320_30Fps },
        };

        /// <summary>
        /// The participants
        /// </summary>
        internal List<IParticipant> participants;

        private readonly IAudioSocket audioSocket;
        private readonly IVideoSocket vbssSocket;
        private readonly IVideoSocket mainVideoSocket;

        private readonly ICall call;

        private readonly List<IVideoSocket> multiViewVideoSockets;

        private readonly ILocalMediaSession mediaSession;
        private readonly IGraphLogger logger;
        private readonly ISLPipeline islPipeline;
        private int shutdown;

        /// <summary>
        /// Initializes a new instance of the <see cref="BotMediaStream" /> class.
        /// </summary>
        /// <param name="mediaSession">he media session.</param>
        /// <param name="callId">The call identity</param>
        /// <param name="logger">The logger.</param>
        /// <param name="eventPublisher">Event Publisher</param>
        /// <param name="settings">Azure settings</param>
        /// <exception cref="InvalidOperationException">A mediaSession needs to have at least an audioSocket</exception>
        public BotMediaStream(
            ILocalMediaSession mediaSession,
            ICall call,
            IGraphLogger logger,
            AzureSettings settings,
            ISLPipeline islPipeline
        )
            : base(logger)
        {
            ArgumentVerifier.ThrowOnNullArgument(mediaSession, nameof(mediaSession));
            ArgumentVerifier.ThrowOnNullArgument(logger, nameof(logger));
            ArgumentVerifier.ThrowOnNullArgument(settings, nameof(settings));

            this.mediaSession = mediaSession;
            this.logger = logger;
            this.islPipeline = islPipeline;
            this.participants = new List<IParticipant>();

            this.call = call;

            // Subscribe to the audio media.
            this.audioSocket = mediaSession.AudioSocket;
            if (this.audioSocket == null)
            {
                throw new InvalidOperationException("A mediaSession needs to have at least an audioSocket");
            }

            this.audioSocket.AudioSendStatusChanged += this.OnAudioSendStatusChanged;
            this.audioSocket.AudioMediaReceived += this.OnAudioMediaReceived;
            this.mainVideoSocket = this.mediaSession.VideoSockets?.FirstOrDefault();
            if (this.mainVideoSocket != null)
            {
                this.mainVideoSocket.VideoSendStatusChanged += this.OnVideoSendStatusChanged;
                this.mainVideoSocket.VideoKeyFrameNeeded += this.OnVideoKeyFrameNeeded;
                this.mainVideoSocket.VideoMediaReceived += this.OnVideoMediaReceived;
            }

            this.multiViewVideoSockets = this.mediaSession.VideoSockets?.ToList();
            foreach (var videoSocket in this.multiViewVideoSockets)
            {
                videoSocket.VideoMediaReceived += this.OnVideoMediaReceived;
                videoSocket.VideoReceiveStatusChanged += this.OnVideoReceiveStatusChanged;
            }

            this.vbssSocket = this.mediaSession.VbssSocket;
            if (this.vbssSocket != null)
            {
                this.vbssSocket.VideoSendStatusChanged += this.OnVbssSocketSendStatusChanged;
                this.vbssSocket.MediaStreamFailure += this.OnVbssMediaStreamFailure;
            }
        }

        /// <summary>
        /// Gets the participants.
        /// </summary>
        /// <returns>List&lt;IParticipant&gt;.</returns>
        public List<IParticipant> GetParticipants()
        {
            return participants;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            // Event Dispose of the bot media stream object
            base.Dispose(disposing);

            if (Interlocked.CompareExchange(ref this.shutdown, 1, 1) == 1)
            {
                return;
            }

            if (this.audioSocket != null)
            {
                this.audioSocket.AudioSendStatusChanged -= this.OnAudioSendStatusChanged;
                this.audioSocket.AudioMediaReceived -= this.OnAudioMediaReceived;
            }

            if (this.mainVideoSocket != null)
            {
                this.mainVideoSocket.VideoKeyFrameNeeded -= this.OnVideoKeyFrameNeeded;
                this.mainVideoSocket.VideoSendStatusChanged -= this.OnVideoSendStatusChanged;
                this.mainVideoSocket.VideoMediaReceived -= this.OnVideoMediaReceived;
            }

            if (this.vbssSocket != null)
            {
                this.vbssSocket.VideoSendStatusChanged -= this.OnVbssSocketSendStatusChanged;
            }
        }

        #region Subscription
        /// <summary>
        /// Subscription for video and vbss.
        /// </summary>
        /// <param name="mediaType">vbss or video.</param>
        /// <param name="mediaSourceId">The video source Id.</param>
        /// <param name="videoResolution">The preferred video resolution.</param>
        /// <param name="socketId">Socket id requesting the video. For vbss it is always 0.</param>
        public void Subscribe(MediaType mediaType, uint mediaSourceId, VideoResolution videoResolution, uint socketId = 0)
        {
            try
            {
                this.ValidateSubscriptionMediaType(mediaType);
                this.logger.Info($"Subscribing to the video source: {mediaSourceId} on socket: {socketId} with the preferred resolution: {videoResolution} and mediaType: {mediaType}");
                if (mediaType == MediaType.Vbss)
                {
                    if (this.vbssSocket == null)
                    {
                        this.logger.Warn($"vbss socket not initialized");
                    }
                    else
                    {
                        this.vbssSocket.Subscribe(videoResolution, mediaSourceId);
                    }
                }
                else if (mediaType == MediaType.Video)
                {
                    if (this.multiViewVideoSockets == null)
                    {
                        this.logger.Warn($"video sockets were not created");
                    }
                    else
                    {
                        this.multiViewVideoSockets[(int)socketId].Subscribe(videoResolution, mediaSourceId);
                    }
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, $"Video Subscription failed for the socket: {socketId} and MediaSourceId: {mediaSourceId} with exception");
            }
        }

        /// <summary>
        /// Unsubscribe to video.
        /// </summary>
        /// <param name="mediaType">vbss or video.</param>
        /// <param name="socketId">Socket id. For vbss it is always 0.</param>
        public void Unsubscribe(MediaType mediaType, uint socketId = 0)
        {
            try
            {
                this.ValidateSubscriptionMediaType(mediaType);
                this.logger.Info($"Unsubscribing to video for the socket: {socketId} and mediaType: {mediaType}");
                if (mediaType == MediaType.Vbss)
                {
                    this.vbssSocket?.Unsubscribe();
                }
                else if (mediaType == MediaType.Video)
                {
                    this.multiViewVideoSockets[(int)socketId]?.Unsubscribe();
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, $"Unsubscribing to video failed for the socket: {socketId} with exception");
            }
        }

        /// <summary>
        /// Ensure media type is video or VBSS.
        /// </summary>
        /// <param name="mediaType">Media type to validate.</param>
        private void ValidateSubscriptionMediaType(MediaType mediaType)
        {
            if (mediaType != MediaType.Vbss && mediaType != MediaType.Video)
            {
                throw new ArgumentOutOfRangeException($"Invalid mediaType: {mediaType}");
            }
        }
        #endregion

        #region Audio
        /// <summary>
        /// Callback for informational updates from the media plaform about audio status changes.
        /// Once the status becomes active, audio can be loopbacked.
        /// </summary>
        /// <param name="sender">The audio socket.</param>
        /// <param name="e">Event arguments.</param>
        private void OnAudioSendStatusChanged(object sender, AudioSendStatusChangedEventArgs e)
        {
            this.logger.Info($"[AudioSendStatusChangedEventArgs(MediaSendStatus={e.MediaSendStatus})]");
        }

        /// <summary>
        /// Receive audio from subscribed participant.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The audio media received arguments.</param>
        private void OnAudioMediaReceived(object sender, AudioMediaReceivedEventArgs e)
        {
            try
            {
                e.Buffer.Dispose();
            }
            catch (Exception ex)
            {
                this.logger.Error(ex);
            }
            finally
            {
                e.Buffer.Dispose();
            }

        }

        /// <summary>
        /// Sends an <see cref="AudioMediaBuffer"/> to the call from the Bot's audio feed.
        /// </summary>
        /// <param name="buffer">The audio buffer to send.</param>
        private void SendAudio(AudioMediaBuffer buffer)
        {
            // Send the audio to our outgoing video stream
            try
            {
                this.audioSocket.Send(buffer);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, $"[OnAudioReceived] Exception while calling audioSocket.Send()");
            }
        }
        #endregion

        #region Video
        /// <summary>
        /// Callback for informational updates from the media plaform about video status changes.
        /// Once the Status becomes active, then video can be received.
        /// </summary>
        /// <param name="sender">The video socket.</param>
        /// <param name="e">Event arguments.</param>
        private void OnVideoReceiveStatusChanged(object sender, VideoReceiveStatusChangedEventArgs e)
        {
            this.logger.Info($"[VideoReceiveStatusChangedEventArgs(MediaReceiveStatus=<{e.MediaReceiveStatus}>]");
        }

        /// <summary>
        /// Choose who to spotlight, and then send their video, when we receive video from a subscribed participant.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The video media received arguments.
        /// </param>
        private void OnVideoMediaReceived(object sender, VideoMediaReceivedEventArgs e)
        {
            // this.logger.Info($"[VideoMediaReceivedEventArgs(Data=<{e.Buffer.Data.ToString()}>, Length={e.Buffer.Length}, Timestamp={e.Buffer.Timestamp}, Width={e.Buffer.VideoFormat.Width}, Height={e.Buffer.VideoFormat.Height}, ColorFormat={e.Buffer.VideoFormat.VideoColorFormat}, FrameRate={e.Buffer.VideoFormat.FrameRate} MediaSourceId={e.Buffer.MediaSourceId})]");
            var participant = BotMediaStream.GetParticipantFromMSI(this.call, e.Buffer.MediaSourceId);
            this.islPipeline.OnVideoMediaReceived(e.Buffer, participant.Resource.Info.Identity.User.Id);
            e.Buffer.Dispose();
        }

        public static IParticipant GetParticipantFromMSI(ICall call, uint msi)
        {
            return call.Participants.SingleOrDefault(x => x.Resource.IsInLobby == false && x.Resource.MediaStreams.Any(y => y.SourceId == msi.ToString()));
        }

        /// <summary>
        /// Sends a <see cref="VideoMediaBuffer"/> to the call from the Bot's video feed.
        /// </summary>
        /// <param name="buffer">The video buffer to send.</param>
        private void SendVideo(VideoMediaBuffer buffer)
        {
            // Send the video to our outgoing video stream
            try
            {
                this.mainVideoSocket.Send(buffer);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, $"[OnVideoMediaReceived] Exception while calling mainVideoSocket.Send()");
            }
        }

        /// <summary>
        /// Callback for informational updates from the media plaform about video status changes.
        /// Once the Status becomes active, then video can be sent.
        /// </summary>
        /// <param name="sender">The video socket.</param>
        /// <param name="e">Event arguments.</param>
        private void OnVideoSendStatusChanged(object sender, VideoSendStatusChangedEventArgs e)
        {
            this.logger.Info($"[VideoSendStatusChangedEventArgs(MediaSendStatus=<{e.MediaSendStatus};PreferredVideoSourceFormat=<{e.PreferredVideoSourceFormat}>]");
        }

        /// <summary>
        /// If the application has configured the VideoSocket to receive encoded media, this
        /// event is raised each time a key frame is needed. Events are serialized, so only
        /// one event at a time is raised to the app.
        /// </summary>
        /// <param name="sender">Video socket.</param>
        /// <param name="e">Event args specifying the socket id, media type and video formats for which key frame is being requested.</param>
        private void OnVideoKeyFrameNeeded(object sender, VideoKeyFrameNeededEventArgs e)
        {
            this.logger.Info($"[VideoKeyFrameNeededEventArgs(MediaType=<{{e.MediaType}}>;SocketId=<{{e.SocketId}}>" +
                             $"VideoFormats=<{string.Join(";", e.VideoFormats.ToList())}>] calling RequestKeyFrame on the videoSocket");
        }
        #endregion

        #region Screen Share
        /// <summary>
        /// Performs action when the vbss socket send status changed event is received.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The video send status changed event arguments.
        /// </param>
        private void OnVbssSocketSendStatusChanged(object sender, VideoSendStatusChangedEventArgs e)
        {
            this.logger.Info($"[VbssSendStatusChangedEventArgs(MediaSendStatus=<{e.MediaSendStatus};PreferredVideoSourceFormat=<{e.PreferredVideoSourceFormat}>]");
        }

        /// <summary>
        /// Called upon VBSS media stream failure.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event args.</param>
        private void OnVbssMediaStreamFailure(object sender, MediaStreamFailureEventArgs e)
        {
            this.logger.Error($"[VbssOnMediaStreamFailure({e})]");
        }
        #endregion
    }
}
