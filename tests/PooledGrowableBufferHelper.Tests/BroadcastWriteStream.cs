using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PooledGrowableBufferHelper.Tests
{
    public class BroadcastWriteStream : Stream
    {
        private readonly Stream _stream1;
        private readonly Stream _stream2;

        public override bool CanRead => false;
        public override bool CanSeek => _stream1.CanSeek && _stream2.CanSeek;
        public override bool CanWrite => true;
        public override bool CanTimeout => false;

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override long Length => throw new NotSupportedException();
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public BroadcastWriteStream(Stream stream1, Stream stream2)
        {
            if (stream1 == null)
            {
                throw new ArgumentNullException(nameof(stream1));
            }
            if (stream2 == null)
            {
                throw new ArgumentNullException(nameof(stream2));
            }
            if (!stream1.CanWrite || !stream2.CanWrite)
            {
                throw new ArgumentException();
            }

            _stream1 = stream1;
            _stream2 = stream2;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long position = _stream1.Seek(offset, SeekOrigin.Begin);
            if (_stream2.Seek(offset, SeekOrigin.Begin) != position)
            {
                throw new Exception("Seek return different position.");
            }
            return position;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _stream1.Write(buffer, offset, count);
            _stream2.Write(buffer, offset, count);
        }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _stream1.Write(buffer);
            _stream2.Write(buffer);
        }
        public override void WriteByte(byte value)
        {
            _stream1.WriteByte(value);
            _stream2.WriteByte(value);
        }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return Task.WhenAll(
                _stream1.WriteAsync(buffer, offset, count, cancellationToken),
                _stream2.WriteAsync(buffer, offset, count, cancellationToken)
                );
        }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return new ValueTask(Task.WhenAll(
                _stream1.WriteAsync(buffer, cancellationToken).AsTask(),
                _stream2.WriteAsync(buffer, cancellationToken).AsTask()
                ));
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public override void Flush()
        {
            _stream1.Flush();
            _stream2.Flush();
        }
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return Task.WhenAll(
                _stream1.FlushAsync(cancellationToken),
                _stream2.FlushAsync(cancellationToken)
                );
        }

    }
}
