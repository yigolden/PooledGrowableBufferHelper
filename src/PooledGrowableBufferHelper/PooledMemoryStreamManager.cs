using System;

namespace PooledGrowableBufferHelper
{
    /// <summary>
    /// A stream manager for constructing <see cref="PooledMemoryStream"/>.
    /// </summary>
    public abstract class PooledMemoryStreamManager
    {
        /// <summary>
        /// A shared implement of <see cref="PooledMemoryStreamManager"/>.
        /// </summary>
        public static PooledMemoryStreamManager Shared { get; } = new DefaultPooledMemoryStreamManager(null);

        /// <summary>
        /// Construct a new <see cref="PooledMemoryStream"/> whose buffer will be managed by this instance.
        /// </summary>
        /// <returns></returns>
        public PooledMemoryStream GetStream()
        {
            return new PooledMemoryStream(this);
        }

        /// <summary>
        /// Allocate or rent a new <see cref="BufferSegment"/>.
        /// </summary>
        /// <param name="length">The minimum length for the buffer.</param>
        /// <returns>The allocated <see cref="BufferSegment"/>. </returns>
        protected abstract BufferSegment AllocateBufferSegment(int length);

        /// <summary>
        /// Free or return a <see cref="BufferSegment"/>
        /// </summary>
        /// <param name="segment">The object to free or return to the pool</param>
        protected abstract void FreeBufferSegment(BufferSegment segment);

        internal BufferSegment Allocate(int length)
        {
            length = Math.Max(length, 0);
            return AllocateBufferSegment(length);
        }

        internal void Free(BufferSegment segment)
        {
            FreeBufferSegment(segment);
        }

    }
}
