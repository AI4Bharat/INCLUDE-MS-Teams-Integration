// <copyright file="BotSettings.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AI4Bharat.ISLBot.Services.Settings
{
    public class BotSettings
    {
        public string PsiStorePath { get; set; }

        public string RecordingFilePath { get; set; }

        public string ModelEndpointUrl { get; set; }

        public ImageDimension Resize { get; set; }

        public int VideoSegmentationIntervalInSeconds { get; set; }

        public bool EnablePsiStore { get; set; }

        public bool EnablePsiDiagnostics { get; set; }
    }

    public class ImageDimension
    {
        public int Height { get; set; }

        public int Width { get; set; }
    }
}
