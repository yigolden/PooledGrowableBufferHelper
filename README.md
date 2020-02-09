# PooledGrowableBufferHelper

A simple library providing reusable MemoryStream implementation and IBufferWriter&lt;byte> implementation. It is suitable for storing temporary byte stream.

The underlying buffer is rented from ArrayPool&lt;byte>.Shared.

## Get Started

Install the latest version from NuGet. (.NET Frameword 4.6+ and .NET Standard 2.0+ are supported.)

```
Install-Package PooledGrowableBufferHelper
```

## Features

* Implements a MemoryStream-like reusable stream, whose underlying buffer is dynamically rented from ArrayPool&lt;byte>.Shared when more space is needed and returned when Close/Dispose is called.
* Uses a linked list to track buffers rented for a stream instance.
* Implements IBufferWriter&lt;byte> interface, which can be used to append data to the end of the stream.
* Provides a ReadOnlySequence&lt;byte> view over the content in the stream.

## Examples

### Use as a temporary buffer (like a MemoryStream)
```csharp
using (PooledMemoryStream ms = PooledMemoryStreamManager.Shared.GetStream())
{
    ms.Write(buffer1, 0, 12);
    ms.Write(buffer2, 0, 12);

    // Seeking to the begining of the stream is very cheep.
    // However, seeking to other random position may not be.
    ms.Seek(0, SeekOrigin.Begin);

    await ms.CopyToAsync(reaponse.Body);
}
// Please make sure Close/Dispose is called so that rented buffer can be returned to the pool

```

### Use as a IBufferWriter&lt;byte>
```csharp
using (PooledMemoryStream ms = PooledMemoryStreamManager.Shared.GetStream())
{
    IBufferWriter<byte> writer = ms;

    Span<byte> span = writer.GetSpan(16);
    span[0] = 'A';
    ...
    span[11] = 'a';
    writer.Advance(12);

    // When swithing usage from IBufferWriter-base API to stream-based API, make sure Advance is called and no buffer is acquired though GetMemory/GetSpan 
    // When swithing usage from stream-based API to IBufferWriter-base API, make sture the position is at the end of the stream.
    byte[] arr = ms.ToArray[];
}
```

### Use ReadOnlySequence&lt;byte> as a view over the buffer
```csharp
using (PooledMemoryStream ms = PooledMemoryStreamManager.Shared.GetStream())
{
    ms.Write(buffer1, 0, 12);

    ReadOnlySequence<byte> sequence = ms.ToReadOnlySequence();

    // Dont't mutate the content when working with ReadOnlySequence<byte>
}
```

### Use as MemoryStream&lt;byte>
If you have a function that accept MemoryStream&lt;byte> instad of Stream&lt;byte>, you can call AsMemoryStream to acquire a MemoryStream wrapper of the original stream.
```csharp
using (PooledMemoryStream ms = PooledMemoryStreamManager.Shared.GetStream())
{
    MemoryStream s = ms.AdMemoryStream(leaveOpen: true);

    SomeFunction(s);
}

void SomeFunction(MemoryStream ms)
{
    ...
}
```