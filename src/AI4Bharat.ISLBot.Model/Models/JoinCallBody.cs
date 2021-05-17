﻿// <copyright file="JoinCallBody.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// </copyright>

namespace AI4Bharat.ISLBot.Model.Models
{
    /// <summary>
    /// The join call body.
    /// </summary>
    public class JoinCallBody
    {
        /// <summary>
        /// Gets or sets the Teams meeting join URL.
        /// </summary>
        /// <value>The join URL.</value>
        public string JoinURL { get; set; }

        /// <summary>
        /// Gets or sets the display name.
        /// Teams client does not allow changing of ones own display name.
        /// If display name is specified, we join as anonymous (guest) user
        /// with the specified display name.  This will put bot into lobby
        /// unless lobby bypass is disabled.
        /// Side note: if display name is specified, the bot will not have
        /// access to UnmixedAudioBuffer in the Skype Media libraries.
        /// </summary>
        /// <value>The display name.</value>
        public string DisplayName { get; set; }
    }
}
