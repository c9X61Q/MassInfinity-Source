// ⚠️ 重写实现 —— 不是还原代码。依据：MC Bedrock / RakNet 公开线格式规范。
namespace MassInfinity.Rewrite.Bedrock;

/// <summary>Bedrock 的变长整数：无符号 LEB128（VarInt / VarLong / UnsignedVarInt）。</summary>
public static class VarInt
{
    public static void WriteUnsigned(Stream s, ulong v)
    {
        do
        {
            var b = (byte)(v & 0x7F);
            v >>= 7;
            if (v != 0) b |= 0x80;
            s.WriteByte(b);
        } while (v != 0);
    }

    public static ulong ReadUnsigned(Stream s)
    {
        ulong result = 0; var shift = 0;
        while (true)
        {
            var b = s.ReadByte();
            if (b < 0) throw new EndOfStreamException();
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return result;
            shift += 7;
            if (shift > 63) throw new InvalidDataException("VarInt 过长");
        }
    }

    /// <summary>有符号 zigzag 变长（Bedrock 的 i32/i64 走这条）。</summary>
    public static void WriteSigned(Stream s, long v)
    {
        var zz = (ulong)((v << 1) ^ (v >> 63));
        WriteUnsigned(s, zz);
    }

    public static long ReadSigned(Stream s)
    {
        var zz = ReadUnsigned(s);
        return (long)(zz >> 1) ^ -(long)(zz & 1);
    }
}
