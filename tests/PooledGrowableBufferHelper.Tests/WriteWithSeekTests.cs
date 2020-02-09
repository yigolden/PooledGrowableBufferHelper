using System;
using System.IO;
using Xunit;

namespace PooledGrowableBufferHelper.Tests
{
    public class WriteWithSeekTests
    {

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WriteWithSeek(bool useSpan)
        {
            var manager = new NonePooledMemoryStreamManager(new PooledMemoryStreamOptions() { MinimumSegmentSize = 16 });

            var ms = new MemoryStream();
            var stream = manager.GetStream();

            var writeStream = new BroadcastWriteStream(ms, stream);

            byte[] buffer = new byte[8];
            buffer.AsSpan().Fill(1);
            if (useSpan)
            {
                writeStream.Write(buffer.AsSpan(0, 8));
            }
            else
            {
                writeStream.Write(buffer, 0, 8);
            }
            
            buffer.AsSpan().Fill(2);
            if (useSpan)
            {
                writeStream.Write(buffer.AsSpan(0, 8));
            }
            else
            {
                writeStream.Write(buffer, 0, 8);
            }

            buffer.AsSpan().Fill(3);
            if (useSpan)
            {
                writeStream.Write(buffer.AsSpan(0, 8));
            }
            else
            {
                writeStream.Write(buffer, 0, 8);
            }

            buffer.AsSpan().Fill(4);
            if (useSpan)
            {
                writeStream.Write(buffer.AsSpan(0, 8));
            }
            else
            {
                writeStream.Write(buffer, 0, 8);
            }

            ms.Seek(24, SeekOrigin.Begin);
            stream.Seek(24, SeekOrigin.Begin);

            buffer.AsSpan().Fill(5);
            if (useSpan)
            {
                writeStream.Write(buffer.AsSpan(0, 4));
            }
            else
            {
                writeStream.Write(buffer, 0, 4);
            }

            AssertHelper.StreamEqual(ms, stream);
        }

    }
}
