using System;
using System.Buffers;

namespace PooledGrowableBufferHelper
{
    /// <summary>
    /// Represents a linked list of byte array nodes.
    /// </summary>
    public sealed class BufferSegment : ReadOnlySequenceSegment<byte>
    {
        private byte[]? _array;
        private int _end;

        /// <summary>
        /// Initialize the node with a byte array buffer. This node will use the byte array as the underlying storage until Reset is called.
        /// </summary>
        /// <param name="array">Byte array to be used by this node.</param>
        public BufferSegment(byte[] array)
        {
            _array = array ?? throw new ArgumentNullException(nameof(array));
            _end = 0;
            Memory = array;
        }

        /// <summary>
        /// Gets the array this node is using.
        /// </summary>
#pragma warning disable CA1819 // Properties should not return arrays
        public byte[] Array => _array ?? throw new InvalidOperationException();
#pragma warning restore CA1819 // Properties should not return arrays

        /// <summary>
        /// Gets the available space in the buffer.
        /// </summary>
        internal int Available => (_array?.Length ?? throw new InvalidOperationException()) - _end;

        /// <summary>
        /// Gets the length of user data in the buffer.
        /// </summary>
        internal int Length { get => _end; set => _end = value; }

        /// <summary>
        /// Gets the total length of the buffer.
        /// </summary>
        internal int Capacity => _array?.Length ?? throw new InvalidOperationException();

        /// <summary>
        /// Gets the next node.
        /// </summary>
        internal new BufferSegment Next => (BufferSegment)base.Next;

        /// <summary>
        /// Sets the sum of node lengths before the current node.
        /// </summary>
        /// <param name="runningIndex">The sum of node lengths before the current node.</param>
        internal void SetRunningIndex(long runningIndex)
        {
            RunningIndex = runningIndex;
        }

        /// <summary>
        /// Sets the next node.
        /// </summary>
        /// <param name="next">The next node.</param>
        internal void SetNext(BufferSegment? next)
        {
            base.Next = next;

            long runningIndex = RunningIndex;
            BufferSegment current = this;

            while (next is not null)
            {
                runningIndex += current._end;
                next.RunningIndex = runningIndex;
                current = next;
                next = next.Next;
            }

        }

        /// <summary>
        /// Clear all states associated with this instance.
        /// </summary>
        public void Reset()
        {
            _array = null;
            _end = 0;
            Memory = default;
            base.Next = null;
            RunningIndex = 0;
        }

    }
}
