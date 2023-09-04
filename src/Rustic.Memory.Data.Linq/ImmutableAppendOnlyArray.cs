using System.Diagnostics;
using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
namespace Rustic.Memory.Data.Linq;

/// <summary>
/// Immutable slice of an array. Supports fast appends by creating new versions, instead of shallow copying the backing array whenever possible.
/// Random inserts will result in a new array being created.
/// </summary>
public readonly struct ImmutableAppendOnlyArray<T>
{
    private readonly SourceBuffer? _source;
    private readonly int _length;

    private ImmutableAppendOnlyArray(ImmutableAppendOnlyArray<T>.SourceBuffer source)
    {
        _source = source;
        _length = source.Length;
    }

    public int Length => _length;

    public T this[int index] => AsSpan()[index];
    public T this[Index index] => AsSpan()[index];
    public ReadOnlySpan<T> this[Range range] => AsSpan()[range];
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> AsSpan() => _source is null ? default : _source.Buffer.Slice(0, _length);
    public ReadOnlySpan<T>.Enumerator GetEnumerator() => AsSpan().GetEnumerator();

    public ImmutableAppendOnlyArray<T> AddRange(ReadOnlySpan<T> values)
    {
        if (values.IsEmpty)
        {
            return this;
        }
        if (_source is null)
        {
            SourceBuffer newSource2 = new();
            var newBuffer2 = newSource2.Reserve(values.Length);
            values.CopyTo(newBuffer2);
            newSource2.Advance(values.Length);
            return new(newSource2);
        }

        if (_length == _source.Length)
        {
            values.CopyTo(_source.Reserve(values.Length));
            _source.Advance(values.Length);
            return new(_source);
        }

        SourceBuffer newSource = new();
        var newBuffer = newSource.Reserve(_source.Length + Math.Max(values.Length, 16));
        _source.Buffer.CopyTo(newBuffer);
        values.CopyTo(newBuffer.Slice(_source.Length));
        newSource.Advance(_source.Length + values.Length);
        return new(newSource);
    }

    public ImmutableAppendOnlyArray<T> Add(T value)
    {
        var valueAsSpan = MemoryMarshal.CreateReadOnlySpan(ref value, 1);
        return AddRange(valueAsSpan);
    }

    internal sealed class SourceBuffer
    {
        private T[]? _buffer;

        public SourceBuffer()
        {
        }

        private int _length;
        public ReadOnlySpan<T> Buffer => _buffer.AsSpan(0, _length);
        public int Length => _length;
        public Span<T> Reserve(int minimumSize)
        {
            Debug.Assert(minimumSize >= 0);
            int requiredCapacity = _length + minimumSize;
            if (_buffer is null)
            {
                _buffer = new T[Math.Max(requiredCapacity, 16)];
                return _buffer.AsSpan(0, minimumSize);
            }
            if (requiredCapacity <= _buffer.Length)
            {
                return _buffer.AsSpan(_length, minimumSize);
            }
            int newCapacity = Math.Max(requiredCapacity, _buffer.Length * 2);
            T[] newBuffer = new T[newCapacity];
            _buffer.CopyTo(newBuffer, 0);
            _buffer = newBuffer;
            return _buffer.AsSpan(_length, minimumSize);
        }

        public void Advance(int count)
        {
            Debug.Assert(count >= 0);
            _length += count;
        }
    }
}