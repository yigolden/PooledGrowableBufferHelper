using System;

namespace PooledGrowableBufferHelper.Tests
{
    public sealed class NonePooledMemoryStreamManager : PooledMemoryStreamManager
    {
        private readonly PooledMemoryStreamOptions _options;

        public NonePooledMemoryStreamManager(PooledMemoryStreamOptions options) : base()
        {
            _options = options;
        }

        protected override BufferSegment AllocateBufferSegment(int length)
        {
            length = Math.Clamp(length, _options.MinimumSegmentSize, _options.MaximumSegmentSize);
            return new BufferSegment(new byte[length]);
        }

        protected override void FreeBufferSegment(BufferSegment segment)
        {
            segment.Reset();
        }
    }
}
