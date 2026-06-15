using System;
using System.Collections.Generic;

// Sequential byte writer for ReliableData payloads. Chainable.
public sealed class PayloadWriter
{
    readonly List<byte> _bytes = new();

    public PayloadWriter WriteByte(byte value)
    {
        _bytes.Add(value);
        return this;
    }

    public PayloadWriter WriteInt(int value)
    {
        _bytes.AddRange(BitConverter.GetBytes(value));
        return this;
    }

    public PayloadWriter WriteFloat(float value)
    {
        _bytes.AddRange(BitConverter.GetBytes(value));
        return this;
    }

    public byte[] ToArray() => _bytes.ToArray();
}

// Sequential byte reader with bounds checks, mirroring PayloadWriter.
public struct PayloadReader
{
    readonly byte[] _array;
    readonly int _end;
    int _pos;

    public PayloadReader(ArraySegment<byte> data)
    {
        _array = data.Array;
        _pos = data.Offset;
        _end = data.Offset + data.Count;
    }

    public bool CanRead(int byteCount) => _array != null && _pos + byteCount <= _end;

    public byte ReadByte()
    {
        byte value = _array[_pos];
        _pos += 1;
        return value;
    }

    public int ReadInt()
    {
        int value = BitConverter.ToInt32(_array, _pos);
        _pos += 4;
        return value;
    }

    public float ReadFloat()
    {
        float value = BitConverter.ToSingle(_array, _pos);
        _pos += 4;
        return value;
    }
}
