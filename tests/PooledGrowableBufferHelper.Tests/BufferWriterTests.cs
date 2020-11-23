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
    }
}
