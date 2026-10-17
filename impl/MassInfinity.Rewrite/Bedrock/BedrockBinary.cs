// ⚠️ 重写实现 —— 不是还原代码。
namespace MassInfinity.Rewrite.Bedrock;

/// <summary>Bedrock 线格式的读写原语（小端 + VarInt 字符串）。</summary>
public sealed class BedrockWriter
{
    private readonly MemoryStream _ms = new();
    public Stream Stream => _ms;

    public BedrockWriter Byte(byte v) { _ms.WriteByte(v); return this; }
    public BedrockWriter Bool(bool v) => Byte(v ? (byte)1 : (byte)0);
    public BedrockWriter VarUInt(ulong v) { VarInt.WriteUnsigned(_ms, v); return this; }
    public BedrockWriter VarSInt(long v) { VarInt.WriteSigned(_ms, v); return this; }

    public BedrockWriter I32Big(int v)
    {
        var b = BitConverter.GetBytes(v);
        if (BitConverter.IsLittleEndian) Array.Reverse(b);
        _ms.Write(b); return this;
    }

    public BedrockWriter I64(long v) { _ms.Write(BitConverter.GetBytes(v)); return this; }

    public BedrockWriter String(string s)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(s ?? string.Empty);
        VarUInt((ulong)bytes.Length);
        _ms.Write(bytes); return this;
    }

    public byte[] ToArray() => _ms.ToArray();
}

/// <summary>对应的读取端。</summary>
public sealed class BedrockReader
{
    private readonly Stream _s;
    public BedrockReader(Stream s) => _s = s;
    public BedrockReader(byte[] b) => _s = new MemoryStream(b, writable: false);

    private void Need(int n)
    {
        // MemoryStream 由调用方保证长度；此处只做边界提示
        if (_s.CanSeek && _s.Length - _s.Position < n) throw new EndOfStreamException($"需要 {n} 字节");
    }

    public byte Byte() { Need(1); var b = _s.ReadByte(); if (b < 0) throw new EndOfStreamException(); return (byte)b; }
    public bool Bool() => Byte() != 0;
    public ulong VarUInt() => VarInt.ReadUnsigned(_s);
    public long VarSInt() => VarInt.ReadSigned(_s);

    public int I32Big()
    {
        Need(4);
        var b = new byte[4];
        _s.ReadExactly(b, 0, 4);
        if (BitConverter.IsLittleEndian) Array.Reverse(b);
        return BitConverter.ToInt32(b, 0);
    }

    public long I64()
    {
        Need(8);
        var b = new byte[8];
        _s.ReadExactly(b, 0, 8);
        return BitConverter.ToInt64(b, 0);
    }

    public string String()
    {
        var n = (int)VarUInt();
        if (n < 0) throw new InvalidDataException("字符串长度为负");
        Need(n);
        var b = new byte[n];
        _s.ReadExactly(b, 0, n);
        return System.Text.Encoding.UTF8.GetString(b);
    }

    public byte[] Rest()
    {
        using var ms = new MemoryStream();
        _s.CopyTo(ms);
        return ms.ToArray();
    }
}
