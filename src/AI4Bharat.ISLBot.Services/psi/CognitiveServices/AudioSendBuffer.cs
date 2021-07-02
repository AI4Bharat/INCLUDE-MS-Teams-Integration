// <copyright file="AudioSendBuffer.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using Microsoft.Skype.Bots.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace AI4Bharat.ISLBot.Services.CognitiveServices
{
    public class AudioSendBuffer : AudioMediaBuffer
    {
        private int _disposed;

        public AudioSendBuffer(IntPtr data, long length, AudioFormat audioFormat, long timeStamp)
        {
            Data = data;
            Length = length;
            AudioFormat = audioFormat;
            Timestamp = timeStamp;
        }

        protected override void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                Marshal.FreeHGlobal(Data);
                Data = IntPtr.Zero;
            }
        }
    }
}
