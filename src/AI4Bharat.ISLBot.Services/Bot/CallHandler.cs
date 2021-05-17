// <copyright file="CallHandler.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using Microsoft.Graph;
using Microsoft.Graph.Communications.Calls;
using Microsoft.Graph.Communications.Calls.Media;
using Microsoft.Graph.Communications.Common.Telemetry;
using Microsoft.Graph.Communications.Resources;
using AI4Bharat.ISLBot.Model.Constants;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Timers;
using AI4Bharat.ISLBot.Service.Settings;
using System.Linq;
using System.Collections.Concurrent;
using Microsoft.Skype.Bots.Media;

namespace AI4Bharat.ISLBot.Services.Bot
{
    /// <summary>
    /// Call Handler Logic.
    /// </summary>
    public class CallHandler : HeartbeatHandler
    {
        /// <summary>
        /// Gets the call.
        /// </summary>
        /// <value>The call.</value>
        public ICall Call { get; }

        /// <summary>
        /// Gets the bot media stream.
        /// </summary>
        /// <value>The bot media stream.</value>
        public BotMediaStream BotMediaStream { get; private set; }

        /// <summary>
        /// The settings
        /// </summary>
        private readonly AzureSettings _settings;

        /// <summary>
        /// The is disposed
        /// </summary>
        private bool _isDisposed = false;

        // hashSet of the available sockets
        private readonly HashSet<uint> availableSocketIds = new HashSet<uint>();

        // this is an LRU cache with the MSI values, we update this Cache with the dominant speaker events
        // this way we can make sure that the muliview sockets are subscribed to the active (speaking) participants
        private readonly LRUCache currentVideoSubscriptions = new LRUCache(BotConstants.NumberOfMultiviewSockets + 1);

        private readonly object subscriptionLock = new object();

        // This dictionnary helps maintaining a mapping of the sockets subscriptions
        private readonly ConcurrentDictionary<uint, uint> msiToSocketIdMapping = new ConcurrentDictionary<uint, uint>();


        /// <summary>
        /// Initializes a new instance of the <see cref="CallHandler" /> class.
        /// </summary>
        /// <param name="statefulCall">The stateful call.</param>
        /// <param name="settings">The settings.</param>
        public CallHandler(
            ICall statefulCall,
            AzureSettings settings
        )
            : base(TimeSpan.FromMinutes(10), statefulCall?.GraphLogger)
        {
            _settings = settings;

            this.Call = statefulCall;
            this.Call.OnUpdated += this.CallOnUpdated;
            this.Call.Participants.OnUpdated += this.ParticipantsOnUpdated;

            foreach (var videoSocket in this.Call.GetLocalMediaSession().VideoSockets)
            {
                this.availableSocketIds.Add((uint)videoSocket.SocketId);
            }

            this.BotMediaStream = new BotMediaStream(this.Call.GetLocalMediaSession(), this.Call.Id, this.GraphLogger,  _settings);
        }

        /// <inheritdoc/>
        protected override Task HeartbeatAsync(ElapsedEventArgs args)
        {
            return this.Call.KeepAliveAsync();
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {

            base.Dispose(disposing);
            _isDisposed = true;
            this.Call.OnUpdated -= this.CallOnUpdated;
            this.Call.Participants.OnUpdated -= this.ParticipantsOnUpdated;

            foreach (var participant in this.Call.Participants)
            {
                participant.OnUpdated -= this.OnParticipantUpdated;
            }

            this.BotMediaStream?.Dispose();
        }

        /// <summary>
        /// Event fired when the call has been updated.
        /// </summary>
        /// <param name="sender">The call.</param>
        /// <param name="e">The event args containing call changes.</param>
        private void CallOnUpdated(ICall sender, ResourceEventArgs<Call> e)
        {
            this.GraphLogger.Info($"Call status updated to {e.NewResource.State} - {e.NewResource.ResultInfo?.Message}");
            // Event - Recording update e.g established/updated/start/ended

            if (e.OldResource.State != e.NewResource.State && e.NewResource.State == CallState.Established)
            {
            }
        }

        /// <summary>
        /// Creates the participant update json.
        /// </summary>
        /// <param name="participantId">The participant identifier.</param>
        /// <param name="participantDisplayName">Display name of the participant.</param>
        /// <returns>System.String.</returns>
        private string CreateParticipantUpdateJson(string participantId, string participantDisplayName = "")
        {
            if (participantDisplayName.Length==0)
                return "{" + String.Format($"\"Id\": \"{participantId}\"") + "}";
            else
                return "{" + String.Format($"\"Id\": \"{participantId}\", \"DisplayName\": \"{participantDisplayName}\"") + "}";
        }

        /// <summary>
        /// Updates the participant.
        /// </summary>
        /// <param name="participants">The participants.</param>
        /// <param name="participant">The participant.</param>
        /// <param name="added">if set to <c>true</c> [added].</param>
        /// <param name="participantDisplayName">Display name of the participant.</param>
        /// <returns>System.String.</returns>
        private string updateParticipant(List<IParticipant> participants, IParticipant participant, bool added, string participantDisplayName = "")
        {
            if (added)
            {
                participants.Add(participant);
                participant.OnUpdated += this.OnParticipantUpdated;
                this.SubscribeToParticipantVideo(participant, forceSubscribe: false);
            }
            else
            {
                participants.Remove(participant);
                participant.OnUpdated -= this.OnParticipantUpdated;
                this.UnsubscribeFromParticipantVideo(participant);
            }

            return CreateParticipantUpdateJson(participant.Id, participantDisplayName);
        }

        /// <summary>
        /// Updates the participants.
        /// </summary>
        /// <param name="eventArgs">The event arguments.</param>
        /// <param name="added">if set to <c>true</c> [added].</param>
        private void UpdateParticipants(ICollection<IParticipant> eventArgs, bool added = true)
        {
            foreach (var participant in eventArgs)
            {
                var json = string.Empty;

                // todo remove the cast with the new graph implementation,
                // for now we want the bot to only subscribe to "real" participants
                var participantDetails = participant.Resource.Info.Identity.User;

                if (participantDetails != null)
                {
                    json = updateParticipant(this.BotMediaStream.participants, participant, added, participantDetails.DisplayName);
                }
                else if (participant.Resource.Info.Identity.AdditionalData?.Count > 0)
                {
                    if (CheckParticipantIsUsable(participant))
                    {
                        json = updateParticipant(this.BotMediaStream.participants, participant, added);
                    }
                }
            }
        }

        /// <summary>
        /// Event fired when the participants collection has been updated.
        /// </summary>
        /// <param name="sender">Participants collection.</param>
        /// <param name="args">Event args containing added and removed participants.</param>
        public void ParticipantsOnUpdated(IParticipantCollection sender, CollectionEventArgs<IParticipant> args)
        {
            UpdateParticipants(args.AddedResources);
            UpdateParticipants(args.RemovedResources, false);
        }

        /// <summary>
        /// Event fired when a participant is updated.
        /// </summary>
        /// <param name="sender">Participant object.</param>
        /// <param name="args">Event args containing the old values and the new values.</param>
        private void OnParticipantUpdated(IParticipant sender, ResourceEventArgs<Participant> args)
        {
        }

        /// <summary>
        /// Unsubscribe and free up the video socket for the specified participant.
        /// </summary>
        /// <param name="participant">Particant to unsubscribe the video.</param>
        private void UnsubscribeFromParticipantVideo(IParticipant participant)
        {
            var participantSendCapableVideoStream = participant.Resource.MediaStreams.Where(x => x.MediaType == Modality.Video &&
              (x.Direction == MediaDirection.SendReceive || x.Direction == MediaDirection.SendOnly)).FirstOrDefault();

            if (participantSendCapableVideoStream != null)
            {
                var msi = uint.Parse(participantSendCapableVideoStream.SourceId);
                lock (this.subscriptionLock)
                {
                    if (this.currentVideoSubscriptions.TryRemove(msi))
                    {
                        if (this.msiToSocketIdMapping.TryRemove(msi, out uint socketId))
                        {
                            this.BotMediaStream.Unsubscribe(MediaType.Video, socketId);
                            this.availableSocketIds.Add(socketId);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks the participant is usable.
        /// </summary>
        /// <param name="p">The p.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        private bool CheckParticipantIsUsable(IParticipant p)
        {
            foreach (var i in p.Resource.Info.Identity.AdditionalData)
                if (i.Key != "applicationInstance" && i.Value is Identity)
                    return true;

            return false;
        }

        /// <summary>
        /// Subscribe to video or vbss sharer.
        /// if we set the flag forceSubscribe to true, the behavior is to subscribe to a video even if there is no available socket left.
        /// in that case we use the LRU cache to free socket and subscribe to the new MSI.
        /// </summary>
        /// <param name="participant">Participant sending the video or VBSS stream.</param>
        /// <param name="forceSubscribe">If forced, the least recently used video socket is released if no sockets are available.</param>
        private void SubscribeToParticipantVideo(IParticipant participant, bool forceSubscribe = true)
        {
            bool subscribeToVideo = false;
            uint socketId = uint.MaxValue;

            // filter the mediaStreams to see if the participant has a video send
            var participantSendCapableVideoStream = participant.Resource.MediaStreams.Where(x => x.MediaType == Modality.Video &&
               (x.Direction == MediaDirection.SendReceive || x.Direction == MediaDirection.SendOnly)).FirstOrDefault();
            if (participantSendCapableVideoStream != null)
            {
                bool updateMSICache = false;
                var msi = uint.Parse(participantSendCapableVideoStream.SourceId);
                lock (this.subscriptionLock)
                {
                    if (this.currentVideoSubscriptions.Count < this.Call.GetLocalMediaSession().VideoSockets.Count)
                    {
                        // we want to verify if we already have a socket subscribed to the MSI
                        if (!this.msiToSocketIdMapping.ContainsKey(msi))
                        {
                            if (this.availableSocketIds.Any())
                            {
                                socketId = this.availableSocketIds.Last();
                                this.availableSocketIds.Remove((uint)socketId);
                                subscribeToVideo = true;
                            }
                        }

                        updateMSICache = true;
                        this.GraphLogger.Info($"[{this.Call.Id}:SubscribeToParticipant(socket {socketId} available, the number of remaining sockets is {this.availableSocketIds.Count}, subscribing to the participant {participant.Id})");
                    }
                    else if (forceSubscribe)
                    {
                        // here we know that all the sockets subscribed to a video we need to update the msi cache,
                        // and obtain the socketId to reuse with the new MSI
                        updateMSICache = true;
                        subscribeToVideo = true;
                    }

                    if (updateMSICache)
                    {
                        this.currentVideoSubscriptions.TryInsert(msi, out uint? dequeuedMSIValue);
                        if (dequeuedMSIValue != null)
                        {
                            // Cache was updated, we need to use the new available socket to subscribe to the MSI
                            this.msiToSocketIdMapping.TryRemove((uint)dequeuedMSIValue, out socketId);
                        }
                    }
                }

                if (subscribeToVideo && socketId != uint.MaxValue)
                {
                    this.msiToSocketIdMapping.AddOrUpdate(msi, socketId, (k, v) => socketId);

                    this.GraphLogger.Info($"[{this.Call.Id}:SubscribeToParticipant(subscribing to the participant {participant.Id} on socket {socketId})");
                    this.BotMediaStream.Subscribe(MediaType.Video, msi, VideoResolution.HD1080p, socketId);
                }
            }

            // vbss viewer subscription
            var vbssParticipant = participant.Resource.MediaStreams.SingleOrDefault(x => x.MediaType == Modality.VideoBasedScreenSharing
            && x.Direction == MediaDirection.SendOnly);
            if (vbssParticipant != null)
            {
                // new sharer
                this.GraphLogger.Info($"[{this.Call.Id}:SubscribeToParticipant(subscribing to the VBSS sharer {participant.Id})");
                this.BotMediaStream.Subscribe(MediaType.Vbss, uint.Parse(vbssParticipant.SourceId), VideoResolution.HD1080p, socketId);
            }
        }
    }
}
