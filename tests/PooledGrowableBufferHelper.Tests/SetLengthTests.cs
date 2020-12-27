using System;
using System.IO;
using Xunit;

namespace PooledGrowableBufferHelper.Tests
{
    public class SetLengthTests
    {
        private static readonly byte[] s_emptyBytes = new byte[16];
        private static readonly NonePooledMemoryStreamManager s_manager = new NonePooledMemoryStreamManager(new PooledMemoryStreamOptions() { MinimumSegmentSize = 16 });


        [Fact]
        public void SetLengthOnEmptyInstance()
        {
            PooledMemoryStream stream = s_manager.GetStream();

            stream.SetLength(10);

            Assert.Equal(10, stream.Length);
            Assert.Equal(16, stream.Capacity);
            Assert.Equal(0, stream.Position);

            byte[] buffer = new byte[11];
            new Random(42).NextBytes(buffer);
            Assert.Equal(10, stream.Read(buffer, 0, 10));
            Assert.True(buffer.AsSpan(0, 10).SequenceEqual(s_emptyBytes.AsSpan(0, 10)));

            stream.Seek(0, SeekOrigin.Begin);
            Assert.Equal(10, stream.Read(buffer, 0, 11));
        }

        [Fact]
        public void ShrinkOnSingleSegment()
        {
            PooledMemoryStream stream = s_manager.GetStream();
            byte[] buffer = new byte[12];
            new Random(42).NextBytes(buffer);
            stream.Write(buffer);

            Assert.Equal(16, stream.Capacity);
            Assert.Equal(12, stream.Length);
            Assert.Equal(12, stream.Position);

            stream.SetLength(8);

            Assert.Equal(16, stream.Capacity);
            Assert.Equal(8, stream.Length);
            Assert.Equal(8, stream.Position);

            stream.Seek(0, SeekOrigin.Begin);

            byte[] buffer2 = new byte[8];
            Assert.Equal(8, stream.Read(buffer2));

            Assert.True(buffer2.AsSpan().SequenceEqual(buffer.AsSpan(0, 8)));
        }

        [Fact]
        public void ExpandOnSingleSegment()
        {
            PooledMemoryStream stream = s_manager.GetStream();
            byte[] buffer = new byte[4];
            new Random(42).NextBytes(buffer);
            stream.Write(buffer);

            Assert.Equal(16, stream.Capacity);
            Assert.Equal(4, stream.Length);
            Assert.Equal(4, stream.Position);

            stream.SetLength(8);

            Assert.Equal(16, stream.Capacity);
            Assert.Equal(8, stream.Length);
            Assert.Equal(4, stream.Position);

            stream.Seek(0, SeekOrigin.Begin);

            byte[] buffer2 = new byte[8];
            Assert.Equal(8, stream.Read(buffer2));

            Assert.True(buffer2.AsSpan(0, 4).SequenceEqual(buffer.AsSpan(0, 4)));
            Assert.True(buffer2.AsSpan(4, 4).SequenceEqual(s_emptyBytes.AsSpan(0, 4)));
        }

        [Fact]
        public void ExpandToTwoSegment()
        {
            PooledMemoryStream stream = s_manager.GetStream();
            byte[] buffer = new byte[12];
            new Random(42).NextBytes(buffer);
            stream.Write(buffer);

            stream.SetLength(24);

            Assert.Equal(32, stream.Capacity);
            Assert.Equal(24, stream.Length);
            Assert.Equal(12, stream.Position);

            stream.Seek(0, SeekOrigin.Begin);

            byte[] buffer2 = new byte[24];
            Assert.Equal(24, stream.Read(buffer2));

            Assert.True(buffer2.AsSpan(0, 12).SequenceEqual(buffer));
            Assert.True(buffer2.AsSpan(12, 12).SequenceEqual(s_emptyBytes.AsSpan(0, 12)));
        }

        [Fact]
        public void ShrinkFromTwoSegment()
        {
            PooledMemoryStream stream = s_manager.GetStream();
            byte[] buffer = new byte[12];
            new Random(42).NextBytes(buffer);
            stream.Write(buffer);
            stream.Write(buffer);

            Assert.Equal(32, stream.Capacity);
            Assert.Equal(24, stream.Length);
            Assert.Equal(24, stream.Position);

            stream.SetLength(12);

            Assert.Equal(16, stream.Capacity);
            Assert.Equal(12, stream.Length);
            Assert.Equal(12, stream.Position);

            stream.Seek(0, SeekOrigin.Begin);

            byte[] buffer2 = new byte[12];
            Assert.Equal(12, stream.Read(buffer2));

            Assert.True(buffer2.AsSpan(0, 12).SequenceEqual(buffer.AsSpan(0, 12)));
        }

        [Fact]
        public void ExpandUsingSmallSourceOnEmptyInstance()
        {
            var manager = new NonePooledMemoryStreamManager(new PooledMemoryStreamOptions() { MinimumSegmentSize = 16, MaximumSegmentSize = 16 });

            PooledMemoryStream stream = manager.GetStream();
            stream.SetLength(32);

            Assert.Equal(32, stream.Length);
            Assert.Equal(0, stream.Position);
            Assert.Equal(32, stream.Capacity);
        }

        [Fact]
        public void ExpandUsingSmallSource()
        {
            var manager = new NonePooledMemoryStreamManager(new PooledMemoryStreamOptions() { MinimumSegmentSize = 16, MaximumSegmentSize = 16 });

            PooledMemoryStream stream = manager.GetStream();
            stream.Write(new byte[10]);
            stream.SetLength(48);

            Assert.Equal(48, stream.Length);
            Assert.Equal(10, stream.Position);
            Assert.Equal(48, stream.Capacity);
        }
    }
}
