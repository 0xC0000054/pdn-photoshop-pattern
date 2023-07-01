/////////////////////////////////////////////////////////////////////////////////
//
// Photoshop Pattern FileType Plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (c) 2012-2020, 2023 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

namespace PatternFileTypePlugin
{
    internal static class PatternConstants
    {
        internal const uint FileSignature = 0x38425054; // 8BPT
        internal const ushort FileVersion = 1;
        internal const uint RecordVersion = 1;
        internal const uint RecordSubVersion = 3;

        internal const int ChannelHeaderSize = 31;

        internal static class AlphaChannelPadding
        {
            internal const int GrayscaleModeSize = 96;
            internal const int RGBModeSize = 88;
        }
    }
}
