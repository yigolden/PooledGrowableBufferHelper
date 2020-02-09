namespace PooledGrowableBufferHelper
{
    /// <summary>
    /// The default <see cref="PooledMemoryStreamManager"/> options.
    /// </summary>
    public class PooledMemoryStreamOptions
    {
        /// <summary>
        /// The minimum length of byte array rented from the pool.
        /// </summary>
        public int MinimumSegmentSize { get; set; } = 4096;

        /// <summary>
        /// The maximum length of byte array rented from the pool.
        /// </summary>
        public int MaximumSegmentSize { get; set; } = 81920;
    }
}
