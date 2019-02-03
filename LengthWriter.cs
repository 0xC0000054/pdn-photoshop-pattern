/////////////////////////////////////////////////////////////////////////////////
//
// Photoshop Pattern FileType Plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (c) 2012-2019 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

// Portions of this file has been adapted from:
/////////////////////////////////////////////////////////////////////////////////
//
// Photoshop PSD FileType Plugin for Paint.NET
// http://psdplugin.codeplex.com/
//
// This software is provided under the MIT License:
//   Copyright (c) 2006-2007 Frank Blumenberg
//   Copyright (c) 2010-2011 Tao Yue
//
// Portions of this file are provided under the BSD 3-clause License:
//   Copyright (c) 2006, Jonas Beckeman
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using System;

namespace PatternFileTypePlugin
{
    internal sealed class LengthWriter : IDisposable
    {
        private readonly long lengthFieldOffset;
        private readonly long startPosition;
        private BigEndianBinaryWriter writer;
        private bool disposed;

        public LengthWriter(BigEndianBinaryWriter writer)
        {
            this.writer = writer;
            // we will write the correct length later, so remember
            // the position
            lengthFieldOffset = writer.BaseStream.Position;
            writer.Write(0xFEEDFEED);

            // remember the start  position for calculation Image
            // resources length
            startPosition = writer.BaseStream.Position;
            disposed = false;
        }

        public void Dispose()
        {
            if (!disposed)
            {
                long endPosition = writer.BaseStream.Position;
                long length = endPosition - startPosition;

                writer.BaseStream.Position = lengthFieldOffset;
                writer.Write((uint)length);

                writer.BaseStream.Position = endPosition;

                disposed = true;
            }
        }
    }
}
