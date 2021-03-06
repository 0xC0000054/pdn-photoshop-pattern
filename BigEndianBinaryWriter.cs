﻿/////////////////////////////////////////////////////////////////////////////////
//
// Photoshop Pattern FileType for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (c) 2012-2020 Nicholas Hayes
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
using System.Drawing;
using System.IO;
using System.Text;

namespace PatternFileTypePlugin
{
    internal sealed class BigEndianBinaryWriter : IDisposable
    {
#pragma warning disable IDE0032 // Disable the 'Use Auto Property' suggestion
        private Stream stream;
#pragma warning restore IDE0032

        private readonly byte[] buffer;
        private readonly bool leaveOpen;

        /// <summary>
        /// Initializes a new instance of the <see cref="BigEndianBinaryWriter"/> class.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="leaveOpen">If set to <c>true</c> leave the stream open when disposing..</param>
        /// <exception cref="ArgumentNullException">stream</exception>
        public BigEndianBinaryWriter(Stream stream, bool leaveOpen)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            buffer = new byte[sizeof(double)];
            this.leaveOpen = leaveOpen;
        }

        /// <summary>
        /// Gets the underlying stream of the <see cref="BigEndianBinaryWriter"/>.
        /// </summary>
        /// <value>
        /// The underlying stream of the <see cref="BigEndianBinaryWriter"/>.
        /// </value>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public Stream BaseStream
        {
            get
            {
                VerifyNotDisposed();

                // Force the stream to write any buffered data.
                stream.Flush();

                return stream;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (!leaveOpen && stream != null)
            {
                stream.Dispose();
                stream = null;
            }
        }

        /// <summary>
        /// Writes a byte to the current stream.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public void Write(byte value)
        {
            VerifyNotDisposed();

            stream.WriteByte(value);
        }

        /// <summary>
        /// Writes a 2-byte signed integer to the current stream in big endian byte order.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public void Write(short value)
        {
            VerifyNotDisposed();

            buffer[0] = (byte)(value >> 8);
            buffer[1] = (byte)value;

            stream.Write(buffer, 0, 2);
        }

        /// <summary>
        /// Writes a 4-byte signed integer to the current stream in big endian byte order.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public void Write(int value)
        {
            VerifyNotDisposed();

            buffer[0] = (byte)(value >> 24);
            buffer[1] = (byte)(value >> 16);
            buffer[2] = (byte)(value >> 8);
            buffer[3] = (byte)value;

            stream.Write(buffer, 0, 4);
        }

        /// <summary>
        /// Writes an 8-byte signed integer to the current stream in big endian byte order.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public void Write(long value)
        {
            VerifyNotDisposed();

            buffer[0] = (byte)(value >> 56);
            buffer[1] = (byte)(value >> 48);
            buffer[2] = (byte)(value >> 40);
            buffer[3] = (byte)(value >> 32);
            buffer[4] = (byte)(value >> 24);
            buffer[5] = (byte)(value >> 16);
            buffer[6] = (byte)(value >> 8);
            buffer[7] = (byte)value;

            stream.Write(buffer, 0, 8);
        }

        /// <summary>
        /// Writes a 2-byte unsigned integer to the current stream in big endian byte order.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public void Write(ushort value)
        {
            VerifyNotDisposed();

            buffer[0] = (byte)(value >> 8);
            buffer[1] = (byte)value;

            stream.Write(buffer, 0, 2);
        }

        /// <summary>
        /// Writes a 4-byte unsigned integer to the current stream in big endian byte order.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public void Write(uint value)
        {
            VerifyNotDisposed();

            buffer[0] = (byte)(value >> 24);
            buffer[1] = (byte)(value >> 16);
            buffer[2] = (byte)(value >> 8);
            buffer[3] = (byte)value;

            stream.Write(buffer, 0, 4);
        }

        /// <summary>
        /// Writes an 8-byte unsigned integer to the current stream in big endian byte order.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public void Write(ulong value)
        {
            VerifyNotDisposed();

            buffer[0] = (byte)(value >> 56);
            buffer[1] = (byte)(value >> 48);
            buffer[2] = (byte)(value >> 40);
            buffer[3] = (byte)(value >> 32);
            buffer[4] = (byte)(value >> 24);
            buffer[5] = (byte)(value >> 16);
            buffer[6] = (byte)(value >> 8);
            buffer[7] = (byte)value;

            stream.Write(buffer, 0, 8);
        }

        /// <summary>
        /// Writes the specified byte array to the current stream.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public void Write(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            VerifyNotDisposed();

            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Writes the specified number of bytes to the current stream, starting from a specified point in the byte array.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <param name="offset">The starting offset in the array.</param>
        /// <param name="count">The count.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public void Write(byte[] bytes, int offset, int count)
        {
            VerifyNotDisposed();

            stream.Write(bytes, offset, count);
        }

        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes a length-prefixed ASCII string.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public void WritePascalString(string value)
        {
            VerifyNotDisposed();

            string str = (value.Length > 255) ? value.Substring(0, 255) : value;
            byte[] bytesArray = Encoding.ASCII.GetBytes(str);

            Write((byte)bytesArray.Length);
            Write(bytesArray);
        }

        /// <summary>
        /// Writes a rectangle comprised of 4-byte signed integers.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public void WriteInt32Rectangle(Rectangle value)
        {
            VerifyNotDisposed();

            Write(value.Top);
            Write(value.Left);
            Write(value.Bottom);
            Write(value.Right);
        }

        /// <summary>
        /// Writes a length-prefixed UTF-16 string.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public void WriteUnicodeString(string value)
        {
            VerifyNotDisposed();

            Write(checked(value.Length + 1));
            Write(Encoding.BigEndianUnicode.GetBytes(value));
            // The string is always null-terminated.
            Write((ushort)0);
        }

        private void VerifyNotDisposed()
        {
            if (stream == null)
            {
                throw new ObjectDisposedException(nameof(BigEndianBinaryReader));
            }
        }
    }
}
