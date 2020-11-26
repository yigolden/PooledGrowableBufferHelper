using System.Buffers;
using Xunit;

namespace PooledGrowableBufferHelper.Tests
{
    public class ReadOnlySequenceTests
    {
        [Fact]
        public void TestSimpleConvertToReadOnlySequence()
        {
            var manager = new NonePooledMemoryStreamManager(new PooledMemoryStreamOptions() { MinimumSegmentSize = 16 });

            PooledMemoryStream stream = manager.GetStream();
            var writer = (IBufferWriter<byte>)stream;
            writer.GetSpan(15);
            writer.Advance(15);
            writer.GetSpan(15);
            writer.Advance(15);

            ReadOnlySequence<byte> sequence = stream.ToReadOnlySequence();
            Assert.Equal(30, sequence.Length);
            Assert.Equal(15, sequence.FirstSpan.Length);
        }
    }
}
