using System;
using System.Buffers;
using Xunit;

namespace PooledGrowableBufferHelper.Tests
{
    public class BufferWriterTests
    {
        [Fact]
        public void DefaultAdvance()
        {
            using PooledMemoryStream stream = PooledMemoryStreamManager.Shared.GetStream();
            var writer = (IBufferWriter<byte>)stream;

            Memory<byte> buffer = writer.GetMemory();
            Assert.False(buffer.IsEmpty);
            writer.Advance(buffer.Length);
            int bytes = buffer.Length;

            buffer = writer.GetMemory();
            Assert.False(buffer.IsEmpty);
            writer.Advance(buffer.Length);

            Assert.Equal(bytes + buffer.Length, stream.Length);
        }

        [Fact]
        public void TestCapacity()
        {
            var manager = new NonePooledMemoryStreamManager(new PooledMemoryStreamOptions() { MinimumSegmentSize = 16 });

            PooledMemoryStream stream = manager.GetStream();
            var writer = (IBufferWriter<byte>)stream;
            writer.GetSpan(15);
            writer.Advance(15);
            Assert.Equal(16, stream.Capacity);

            writer.GetSpan(15);
            writer.Advance(15);
            Assert.Equal(31, stream.Capacity);

            writer.GetSpan(15);
            writer.Advance(15);
            Assert.Equal(46, stream.Capacity);
        }
    }
}
