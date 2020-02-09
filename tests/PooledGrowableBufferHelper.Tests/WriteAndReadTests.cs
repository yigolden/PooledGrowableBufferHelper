using System;
using System.IO;
using Xunit;

namespace PooledGrowableBufferHelper.Tests
{
    public class WriteAndReadTests
    {
        [Theory()]
        [InlineData(42, 2048, 819200)]
        [InlineData(42, 8192, 819200)]
        [InlineData(64, 2048, 819200)]
        [InlineData(64, 8192, 819200)]
        public void RandomStreamWriteAndReadTest(int seed, int maxChunkSize, int minTotalSize)
        {
            var random = new Random(seed);

            var ms = new MemoryStream();
            var stream = new PooledMemoryStream();
            var writeStream = new BroadcastWriteStream(ms, stream);

            int length = 0;
            byte[] buffer = new byte[maxChunkSize];

            bool useSpan = false;

            while (length < minTotalSize)
            {
                int chunkSize = random.Next(1, maxChunkSize);
                random.NextBytes(buffer);

                if (useSpan)
                {
                    writeStream.Write(buffer.AsSpan(0, chunkSize));
                }
                else
                {
                    writeStream.Write(buffer, 0, chunkSize);
                }
                useSpan = !useSpan;
                length += chunkSize;
            }

            AssertHelper.StreamEqual(ms, stream);
        }

        [Fact]
        public void SimpleWriteAndRead()
        {
            var random = new Random(42);

            var manager = new NonePooledMemoryStreamManager(new PooledMemoryStreamOptions() { MinimumSegmentSize = 16 });

            var ms = new MemoryStream();
            var stream = manager.GetStream();
            var writeStream = new BroadcastWriteStream(ms, stream);

            var buffer = new byte[32];
            random.NextBytes(buffer);

            writeStream.Write(buffer, 0, 4);
            writeStream.Write(buffer, 0, 8);
            writeStream.Write(buffer, 0, 12);
            writeStream.Write(buffer, 0, 16);
            writeStream.Write(buffer, 0, 32);

            writeStream.Write(buffer.AsSpan(0, 4));
            writeStream.Write(buffer.AsSpan(0, 8));
            writeStream.Write(buffer.AsSpan(0, 12));
            writeStream.Write(buffer.AsSpan(0, 16));
            writeStream.Write(buffer.AsSpan(0, 32));

            AssertHelper.StreamEqual(ms, stream);
        }
    }
}
