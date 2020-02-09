using System;
using System.IO;
using Xunit;

namespace PooledGrowableBufferHelper.Tests
{
    public static class AssertHelper
    {
        public static void StreamEqual(MemoryStream expected, Stream actual)
        {
            Assert.Equal(expected.Length, actual.Length);
            Assert.True(expected.TryGetBuffer(out ArraySegment<byte> arraySegment));

            var expectedBytes = new ReadOnlySpan<byte>(arraySegment.Array, arraySegment.Offset, arraySegment.Count);

            var actrualBytes = new byte[expected.Length];
            actual.Seek(0, SeekOrigin.Begin);
            Assert.Equal(expected.Length, actual.Read(actrualBytes, 0, (int)expected.Length));

            Assert.True(expectedBytes.SequenceEqual(actrualBytes));
        }
    }
}
