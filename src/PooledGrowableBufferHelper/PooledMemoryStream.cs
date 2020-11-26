using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PooledGrowableBufferHelper
{
    /// <summary>
    /// The reusable memory stream that can be used to write temporary data, or used as a <see cref="IBufferWriter{T}"/>
    /// </summary>
    public sealed class PooledMemoryStream : Stream, IBufferWriter<byte>
    {
        private readonly PooledMemoryStreamManager _manager;

        /// <summary>
        /// Initialize a default reusable memory stream.
        /// </summary>
        public PooledMemoryStream() : this(PooledMemoryStreamManager.Shared) { }

        /// <summary>
        /// Initialize the memory stream using the provided stream manager.
        /// </summary>
        /// <param name="manager">The stream manager.</param>
        public PooledMemoryStream(PooledMemoryStreamManager manager)
        {
            _manager = manager;
        }

        private BufferSegment? _head;
        private BufferSegment? _current;

        private long _position;
        private long _length;
        private long _capacity;

        /// <summary>
        /// Gets or sets the position within the current stream.
        /// </summary>
        public override long Position { get => _position; set => Seek(value, SeekOrigin.Begin); }

        /// <summary>
        /// Gets the number of bytes allocated for this stream.
        /// </summary>
        public long Capacity { get => _capacity; set => throw new NotSupportedException(); }

        /// <summary>
        /// Gets the length of the stream in bytes.
        /// </summary>
        public override long Length => _length;

        /// <summary>
        /// Gets a value indicating whether the current stream supports reading.
        /// </summary>
        public override bool CanRead => true;

        /// <summary>
        /// Gets a value indicating whether the current stream supports writing.
        /// </summary>
        public override bool CanWrite => true;

        /// <summary>
        /// Gets a value indicating whether the current stream supports seeking.
        /// </summary>
        public override bool CanSeek => true;

        /// <summary>
        /// Gets a value that determines whether the current stream can time out.
        /// </summary>
        public override bool CanTimeout => false;

        private BufferSegment EnsureCurrentInitialized(int length)
        {
            if (_head is null)
            {
                BufferSegment current = _head = _current = _manager.Allocate(length);
                current.SetRunningIndex(0);
                _capacity = current.Capacity;
                return current;
            }

            Debug.Assert(_current is not null);
            return _current!;
        }

        private BufferSegment AllocateAndAppendSegment(BufferSegment current, int length)
        {
            // Allocate next buffer segment.
            BufferSegment next = _manager.Allocate(length);
            current.SetNext(next);
            _capacity += next.Capacity - current.Available;
            _current = next;
            return next;
        }

        /// <summary>
        /// Sets the position within the current stream to the specified value.
        /// </summary>
        /// <param name="offset">The new position within the stream. This is relative to the loc parameter, and can be positive or negative.</param>
        /// <param name="loc">A value of type <see cref="SeekOrigin"/>, which acts as the seek reference point.</param>
        /// <returns>The new position within the stream, calculated by combining the initial reference point and the offset.</returns>
        public override long Seek(long offset, SeekOrigin loc)
        {
            switch (loc)
            {
                case SeekOrigin.Begin:
                    break;
                case SeekOrigin.Current:
                    offset = _position + offset;
                    break;
                case SeekOrigin.End:
                    offset = _length + offset;
                    break;
                default:
                    throw new ArgumentException("Unknown SeekOrigin.", nameof(loc));
            }

            if ((ulong)offset > (ulong)_length)
            {
                throw new IOException("New position is not within stream.");
            }

            BufferSegment? last = _head;
            BufferSegment? current = _head;
            while (current is not null && current.RunningIndex < offset)
            {
                last = current;
                current = current.Next;
            }

            _position = offset;
            _current = last;
            return offset;
        }

        private static void CheckParameters(byte[]? buffer, int offset, int count)
        {
            if (buffer is null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            if (buffer.Length - offset < count)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
        }

        /// <summary>
        /// Writes a block of bytes to the current stream using data read from a buffer.
        /// </summary>
        /// <param name="buffer">The buffer to write data from.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream.</param>
        /// <param name="count">The maximum number of bytes to write.</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            CheckParameters(buffer, offset, count);
            Debug.Assert(buffer is not null);

            BufferSegment current = EnsureCurrentInitialized(count);

            // Write to BufferSegment until all the data is written.
            while (count > 0)
            {
                // First we try to override the existing data in the current BufferSegment.
                int segmentOffset = unchecked((int)(_position - current.RunningIndex));
                int segmentAvailable = current.Length - segmentOffset;
                int writeCount = Math.Min(segmentAvailable, count);

                // There are existing data in the current segment, override them
                if (writeCount != 0)
                {
                    Buffer.BlockCopy(buffer, offset, current.Array, segmentOffset, writeCount);
                    _position += writeCount;

                    // Continue writing the remaining data
                    offset += writeCount;
                    count -= writeCount;
                    continue;
                }

                // The remaing space in the current segment.
                segmentAvailable = current.Available;

                // We only copy into the current segment when this is the last segment.
                // If we write to the remaining space regardless, we may break the ReadOnleSequence implementation.
                if (segmentAvailable > 0 && current.Next is null)
                {
                    writeCount = Math.Min(segmentAvailable, count);

                    // Copy the data into the segment
                    Buffer.BlockCopy(buffer, offset, current.Array, segmentOffset, writeCount);
                    current.Length += writeCount;
                    _position += writeCount;
                    _length += writeCount;

                    // Continue writing the remaining data
                    offset += writeCount;
                    count -= writeCount;
                    continue;
                }

                // Move to the next segment, or allocate one.
                current = current.Next ?? AllocateAndAppendSegment(current, count);
            }

            _current = current;
        }

        /// <summary>
        /// Reads a block of bytes from the current stream and writes the data to a buffer.
        /// </summary>
        /// <param name="buffer">When this method returns, contains the specified byte array with the values between offset and (<paramref name="offset"/> + <paramref name="count"/> - 1) replaced by the characters read from the current stream.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin storing data from the current stream.</param>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <returns>The total number of bytes written into the buffer. This can be less than the number of bytes requested if that number of bytes are not currently available, or zero if the end of the stream is reached before any bytes are read.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckParameters(buffer, offset, count);
            Debug.Assert(buffer is not null);

            BufferSegment? current = _current;
            if (current is null)
            {
                return 0;
            }

            Debug.Assert(_position >= current.RunningIndex);
            Debug.Assert(_position <= (current.RunningIndex + current.Length));

            // currentAvailable represents the existing byte count in the current segment.
            int readCount = 0;
            int currentAvailable = (int)(current.RunningIndex + current.Length - _position);

            // Loop until the data in the current segment is sufficient.
            while (currentAvailable <= count)
            {
                // Copy the data from current segment
                Buffer.BlockCopy(current.Array, (int)(_position - current.RunningIndex), buffer, offset, currentAvailable);
                _position += currentAvailable;
                readCount += currentAvailable;
                offset += currentAvailable;
                count -= currentAvailable;

                // Move to next segment
                if (current.Next is null)
                {
                    _current = current;
                    return readCount;
                }
                current = current.Next;
                currentAvailable = (int)(current.RunningIndex + current.Length - _position);
            }

            // Copy the remaining data
            Debug.Assert(current.Next != null);
            Buffer.BlockCopy(current.Array, (int)(_position - current.RunningIndex), buffer, offset, count);
            readCount += count;
            _position += count;
            _current = current;

            return readCount;
        }

        /// <summary>
        /// Returns the byte array instance of the underlying storage. Only succeeds when there is only one BufferSegment is allocated.
        /// </summary>
        /// <param name="buffer">The byte array instance of the underlying storage.</param>
        /// <returns>true if the conversion was successful; otherwise, false.</returns>
        public bool TryGetBuffer(out ArraySegment<byte> buffer)
        {
            BufferSegment? head = _head;
            if (ReferenceEquals(head, _current))
            {
                if (head is null)
                {
                    buffer = new ArraySegment<byte>(Array.Empty<byte>());
                    return true;
                }

                buffer = new ArraySegment<byte>(head.Array, 0, head.Length);
                return true;
            }

            buffer = default;
            return false;
        }

        /// <summary>
        /// Sets the length of the current stream to the specified value.
        /// </summary>
        /// <param name="value">The value at which to set the length.</param>
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// </summary>
        public override void Flush() { }

        /// <summary>
        /// </summary>
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <summary>
        /// </summary>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int readCount = Read(buffer, offset, count);
            return Task.FromResult(readCount);
        }

        /// <summary>
        /// </summary>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }

        /// <summary>
        /// </summary>
        public override void WriteByte(byte value)
        {
            BufferSegment current = EnsureCurrentInitialized(1);

            while (true)
            {
                // First we try to override the existing data in the current BufferSegment.
                int segmentOffset = unchecked((int)(_position - current.RunningIndex));
                int segmentAvailable = current.Length - segmentOffset;

                // There are existing data in the current segment, override them
                if (segmentAvailable >= 1)
                {
                    current.Array[segmentOffset] = value;
                    _position += 1;
                    break;
                }

                // The remaing space in the current segment.
                segmentAvailable = current.Available;

                // We only copy into the current segment when this is the last segment.
                // If we write to the remaining space regardless, we may break the ReadOnleSequence implementation.
                if (segmentAvailable > 0 && current.Next is null)
                {
                    // Write the byte into the segment
                    current.Array[segmentOffset] = value;
                    current.Length += 1;
                    _position += 1;
                    _length += 1;
                    break;
                }

                // Move to the next segment, or allocate one.
                current = current.Next ?? AllocateAndAppendSegment(current, 1);
            }

            _current = current;
        }

        /// <summary>
        /// Asynchronously reads all the bytes from the current stream and writes them to another stream, using a specified buffer size and cancellation token.
        /// </summary>
        /// <param name="destination">The stream to which the contents of the current stream will be copied.</param>
        /// <param name="bufferSize">The size, in bytes, of the buffer. This value must be greater than zero.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous copy operation.</returns>
        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            if (destination is null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            BufferSegment? current = _current;
            if (current is null)
            {
                return;
            }
            long position = _position;

            // Copy until we reached the last BufferSegment
            while (current is not null)
            {
                // Calculate the data offset in this segment. 
                int offset = (int)(position - current.RunningIndex);
                int count = current.Length - offset;

                // Write to the destination stream.
#if FAST_SPAN
                await destination.WriteAsync(current.Array.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
#else
                await destination.WriteAsync(current.Array, offset, count, cancellationToken).ConfigureAwait(false);
#endif

                // Move forward.
                position += count;
                current = current.Next;
            }
        }

        /// <summary>
        /// Writes the entire contents of this memory stream to another stream.
        /// </summary>
        /// <param name="stream">The stream to write this memory stream to.</param>
        public void WriteTo(Stream stream)
            => WriteTo(stream, true);

        private void WriteTo(Stream stream, bool ignorePosition)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            BufferSegment? current;
            long position;
            if (ignorePosition)
            {
                current = _head;
                position = 0;
            }
            else
            {
                current = _current;
                position = _position;
            }

            // Copy until we reached the last BufferSegment
            while (current is not null)
            {
                // Calculate the data offset in this segment. 
                int offset = (int)(position - current.RunningIndex);
                int count = current.Length - offset;

                // Write to the destination stream.
                stream.Write(current.Array, offset, count);

                // Move forward.
                position += count;
                current = current.Next;
            }
        }

        /// <summary>
        /// Writes the stream contents to a byte array, regardless of the Position property.
        /// </summary>
        /// <returns>A new byte array.</returns>
        public byte[] ToArray()
        {
            // 获取当前段
            BufferSegment? current = _head;
            if (current is null)
            {
                return Array.Empty<byte>();
            }
            int offset = 0;
            int count = checked((int)_length);
            byte[] buffer = new byte[_length];

            // currentAvailable represents the existing byte count in the current segment.
            int readCount = 0;
            int currentAvailable = current.Length;

            // Loop until the data in the current segment is sufficient.
            while (currentAvailable <= count)
            {
                // Copy the data from current segment.
                Buffer.BlockCopy(current.Array, 0, buffer, offset, currentAvailable);
                readCount += currentAvailable;
                offset += currentAvailable;
                count -= currentAvailable;

                // Move to next segment.
                if (current.Next is null)
                {
                    return buffer;
                }
                current = current.Next;
                currentAvailable = current.Length;
            }

            // Copy the remaining data.
            Debug.Assert(current.Next != null);
            Buffer.BlockCopy(current.Array, 0, buffer, offset, count);

            return buffer;
        }

        /// <summary>
        /// Closes the stream for reading and writing. Although no error will be thrown, you should now use this object any more.
        /// </summary>
        public override void Close()
        {
            BufferSegment? head = _head;

            _head = null;
            _current = null;
            _position = 0;
            _length = 0;
            _capacity = 0;

            if (head is null)
            {
                return;
            }

            BufferSegment next;
            while (head is not null)
            {
                next = head.Next;
                _manager.Free(head);
                head = next;
            }
        }

        /// <summary>
        /// Wrap the stream in an <see cref="MemoryStream"/> derived instance.
        /// </summary>
        /// <param name="leaveOpen">False if the stream should be disposed when the returned <see cref="MemoryStream"/> is disposed. Otherwise, true.</param>
        /// <returns>The wrapped stream.</returns>
        public MemoryStream AsMemoryStream(bool leaveOpen)
        {
            return new MemoryStreamAdapter(this, leaveOpen);
        }


#if FAST_SPAN

        /// <summary>
        /// </summary>
        public override int Read(Span<byte> buffer)
        {
            // This is basically the same implementation in Read(byte[], int, int)

            int count = buffer.Length;

            BufferSegment? current = _current;
            if (current is null)
            {
                return 0;
            }

            Debug.Assert(_position >= current.RunningIndex);
            Debug.Assert(_position <= (current.RunningIndex + current.Length));

            int readCount = 0;
            int currentAvailable = (int)(current.RunningIndex + current.Length - _position);

            while (currentAvailable <= count)
            {
                current.Array.AsSpan((int)(_position - current.RunningIndex), currentAvailable).CopyTo(buffer);
                _position += currentAvailable;
                readCount += currentAvailable;
                buffer = buffer.Slice(currentAvailable);
                count -= currentAvailable;

                if (current.Next is null)
                {
                    _current = current;
                    return readCount;
                }
                current = current.Next;
                currentAvailable = (int)(current.RunningIndex + current.Length - _position);
            }

            Debug.Assert(current.Next != null);
            current.Array.AsSpan((int)(_position - current.RunningIndex), count).CopyTo(buffer);
            readCount += count;
            _position += count;
            _current = current;

            return readCount;
        }

        /// <summary>
        /// </summary>
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            // This is basically the same implementation in Write(byte[], int, int)

            int count = buffer.Length;

            BufferSegment current = EnsureCurrentInitialized(count);

            while (count > 0)
            {
                int segmentOffset = unchecked((int)(_position - current.RunningIndex));
                int segmentAvailable = current.Length - segmentOffset;
                int writeCount = Math.Min(segmentAvailable, count);

                if (writeCount != 0)
                {
                    buffer.Slice(0, writeCount).CopyTo(current.Array.AsSpan(segmentOffset, writeCount));
                    _position += writeCount;
                    count -= writeCount;
                    buffer = buffer.Slice(writeCount);
                    continue;
                }

                if (count == 0)
                {
                    break;
                }

                segmentAvailable = current.Available;
                if (segmentAvailable > 0 && current.Next is null)
                {
                    writeCount = Math.Min(segmentAvailable, count);

                    buffer.Slice(0, writeCount).CopyTo(current.Array.AsSpan(segmentOffset, writeCount));
                    current.Length += writeCount;
                    _position += writeCount;
                    _length += writeCount;
                    count -= writeCount;
                    buffer = buffer.Slice(writeCount);
                    continue;
                }

                current = current.Next ?? AllocateAndAppendSegment(current, count);
            }

            _current = current;
        }

        /// <summary>
        /// </summary>
        public override void CopyTo(Stream? destination, int bufferSize)
        {
            if (destination is null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            WriteTo(destination, false);
        }

#endif

#region IBufferWriter Implementation

        private const int DefaultBufferSize = 16384;

        /// <summary>
        /// Returns a <see cref="Memory{Byte}"/> to write to that is at least the requested size (specified by sizeHint).
        /// </summary>
        /// <param name="sizeHint">The minimum length of the returned <see cref="Memory{Byte}"/>. If 0, a non-empty buffer is returned.</param>
        /// <returns>A <see cref="Memory{Byte}"/> of at least the size sizeHint. If sizeHint is 0, returns a non-empty buffer.</returns>
        Memory<byte> IBufferWriter<byte>.GetMemory(int sizeHint)
        {
            if (_position != _length)
            {
                throw new InvalidOperationException("IBufferWriter implementation is only usable when position is at the end of the stream.");
            }

            if (sizeHint <= 0)
            {
                sizeHint = DefaultBufferSize;
            }

            BufferSegment current = EnsureCurrentInitialized(sizeHint);

            if (current.Available >= sizeHint)
            {
                return current.Array.AsMemory(current.Length);
            }

            _current = current = AllocateAndAppendSegment(current, sizeHint);

            Debug.Assert(current.Available >= sizeHint);
            return current.Array.AsMemory(current.Length);
        }

        /// <summary>
        /// Returns a <see cref="Span{Byte}"/> to write to that is at least the requested size (specified by sizeHint).
        /// </summary>
        /// <param name="sizeHint">The minimum length of the returned <see cref="Span{Byte}"/>. If 0, a non-empty buffer is returned.</param>
        /// <returns>A <see cref="Span{Byte}"/> of at least the size sizeHint. If sizeHint is 0, returns a non-empty buffer.</returns>
        Span<byte> IBufferWriter<byte>.GetSpan(int sizeHint)
        {
            if (_position != _length)
            {
                throw new InvalidOperationException("IBufferWriter implementation is only usable when position is at the end of the stream.");
            }

            if (sizeHint <= 0)
            {
                sizeHint = DefaultBufferSize;
            }

            BufferSegment current = EnsureCurrentInitialized(sizeHint);

            if (current.Available >= sizeHint)
            {
                return current.Array.AsSpan(current.Length);
            }

            _current = current = AllocateAndAppendSegment(current, sizeHint);

            Debug.Assert(current.Available >= sizeHint);
            return current.Array.AsSpan(current.Length);
        }

        /// <summary>
        /// Notifies the <see cref="IBufferWriter{Byte}"/> that count data items were written to the output <see cref="Span{Byte}"/> or <see cref="Memory{Byte}"/>.
        /// </summary>
        /// <param name="count">The number of data items written to the <see cref="Span{Byte}"/> or <see cref="Memory{Byte}"/>.</param>
        void IBufferWriter<byte>.Advance(int count)
        {
            if (_position != _length)
            {
                throw new InvalidOperationException("IBufferWriter implementation is only usable when position is at the end of the stream.");
            }

            BufferSegment? current = _current;
            if (current is null)
            {
                throw new InvalidOperationException();
            }

            if (current.Available < count)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            current.Length += count;
            _position += count;
            _length += count;
        }

        /// <summary>
        /// Gets the <see cref="ReadOnlySequence{T}"/> representation of the entire stream.
        /// </summary>
        /// <returns>The <see cref="ReadOnlySequence{T}"/> representation of the entire stream.</returns>
        public ReadOnlySequence<byte> ToReadOnlySequence()
        {
            BufferSegment? head = _head;
            if (head is null)
            {
                return ReadOnlySequence<byte>.Empty;
            }

            Debug.Assert(_current is not null);
            BufferSegment current = _current!;
            int endIndex;

            while (current.Next is not null)
            {
                current = current.Next;
            }
            endIndex = current.Length;

            return new ReadOnlySequence<byte>(head, 0, current, endIndex);
        }
#endregion

#region MemoryStream Adapter

        internal class MemoryStreamAdapter : MemoryStream
        {
            private readonly PooledMemoryStream _innerStream;
            private readonly bool _leaveOpen;

            public MemoryStreamAdapter(PooledMemoryStream innerStream, bool leaveOpen)
            {
                Debug.Assert(innerStream != null);
                _innerStream = innerStream!;
                _leaveOpen = leaveOpen;
            }

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => true;
            public override bool CanTimeout => false;

            public override int Capacity { get => (int)_innerStream.Capacity; set => _innerStream.Capacity = value; }
            public override long Length => _innerStream.Length;
            public override long Position { get => _innerStream.Position; set => _innerStream.Position = value; }
            public override int ReadTimeout { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override int WriteTimeout { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
                => _innerStream.CopyToAsync(destination, bufferSize, cancellationToken);
            public override void Flush()
                => _innerStream.Flush();
            public override Task FlushAsync(CancellationToken cancellationToken)
                => _innerStream.FlushAsync(cancellationToken);

            public override int Read(byte[] buffer, int offset, int count)
                => _innerStream.Read(buffer, offset, count);
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
            public override int ReadByte()
                => _innerStream.ReadByte();

            public override long Seek(long offset, SeekOrigin loc)
                => _innerStream.Seek(offset, loc);

            public override void SetLength(long value)
                => _innerStream.SetLength(value);

            public override byte[] GetBuffer()
                => throw new NotSupportedException();

            public override byte[] ToArray()
                => _innerStream.ToArray();

            public override bool TryGetBuffer(out ArraySegment<byte> buffer)
                => _innerStream.TryGetBuffer(out buffer);

            public override void Write(byte[] buffer, int offset, int count)
                => _innerStream.Write(buffer, offset, count);
            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => _innerStream.WriteAsync(buffer, offset, count, cancellationToken);
            public override void WriteByte(byte value)
                => _innerStream.WriteByte(value);

            public override void WriteTo(Stream stream)
                => _innerStream.WriteTo(stream, true);

            public override void Close()
            {
                if (!_leaveOpen)
                {
                    _innerStream.Close();
                }
            }

#if FAST_SPAN

            public override void CopyTo(Stream destination, int bufferSize)
                => _innerStream.CopyTo(destination, bufferSize);

            public override int Read(Span<byte> destination)
                => _innerStream.Read(destination);
            public override ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
                => _innerStream.ReadAsync(destination, cancellationToken);

            public override void Write(ReadOnlySpan<byte> source)
                => _innerStream.Write(source);
            public override ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
                => _innerStream.WriteAsync(source, cancellationToken);
#endif
        }

#endregion
    }
}
