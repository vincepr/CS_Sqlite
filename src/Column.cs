using System.Buffers.Binary;
using System.Text;

namespace CS_Sqlite;

public enum SerialType
{
    Null,
        
    Int8,
    Int16,
    Int24,
    Int32,
    Int48,
    Int64,
        
    Float64,
    Zero,       // Sqlite's false?
    One,        // Sqlite's true?
    Blob,
    Text
}
    // SerialType      ContentSize  Meaning
    // 0	                0       Value is a NULL.
    // 1	                1       Value is an 8-bit twos-complement integer.
    // 2	                2       Value is a big-endian 16-bit twos-complement integer.
    // 3	                3       Value is a big-endian 24-bit twos-complement integer.
    // 4	                4       Value is a big-endian 32-bit twos-complement integer.
    // 5	                6       Value is a big-endian 48-bit twos-complement integer.
    // 6	                8       Value is a big-endian 64-bit twos-complement integer.
    // 7	                8       Value is a big-endian IEEE 754-2008 64-bit floating point number.
    // 8	                0       Value is the integer 0. (Only available for schema format 4 and higher.)
    // 9	                0       Value is the integer 1. (Only available for schema format 4 and higher.)
    // 10,11            variable	Reserved for internal use. These serial type codes will never appear in a well-formed database file, but they might be used in transient and temporary database files that SQLite sometimes generates for its own use. The meanings of these codes can shift from one release of SQLite to the next.
    // N≥12 and even	(N-12)/2	Value is a BLOB that is (N-12)/2 bytes in length.
    // N≥13 and odd	    (N-13)/2	Value is a string in the text encoding and (N-13)/2 bytes in length. The nul terminator is not stored.
public record Column(SerialType Type, byte[] Bytes)
    {
        public static Column Read(Int64 serialType, byte[] bytes)
        {
            static int GetLenBlob(Int64 serialType) => (int)(serialType - 13) / 2;
            static int GetLenText(Int64 serialType) => (int)(serialType - 13) / 2;
            return serialType switch
            {
                0 => new(SerialType.Null, Array.Empty<byte>()),
                1 => new(SerialType.Int8, bytes[..1]),
                2 => new(SerialType.Int16, bytes[..2]),
                3 => new(SerialType.Int24, bytes[..3]),
                4 => new(SerialType.Int32, bytes[..4]),
                5 => new(SerialType.Int48, bytes[..6]),
                6 => new(SerialType.Int64, bytes[..8]),
                7 => new(SerialType.Float64, bytes[..8]),
                8 => new(SerialType.Zero, Array.Empty<byte>()),
                9 => new(SerialType.One, Array.Empty<byte>()),
                <= 11 => throw new InvalidDataException("Unexpected Data Type: Reserved for internal use."),
                var odd when odd%2==1 => new(SerialType.Text, bytes[..GetLenText(odd)]),
                var even => new (SerialType.Blob, bytes[..GetLenBlob(even)]),
            };
        }

        public Int64 ToInt()
        {
            return Type switch
            {
                SerialType.Int8 => (Int64)Bytes[0],
                SerialType.Int16 => BinaryPrimitives.ReadInt16BigEndian(Bytes),
                // SerialType.Int24 => ReadInt24BigEndian(Bytes),
                SerialType.Int32 => BinaryPrimitives.ReadInt32BigEndian(Bytes),
                // SerialType.Int48 => ReadInt48BigEndian(Bytes),
                SerialType.Int64 => BinaryPrimitives.ReadInt64BigEndian(Bytes),
                _ => throw new InvalidCastException("Cant convert column type"),
            };
        }

        public double ToFloat()
        {
            return Type switch
            {
                SerialType.Float64 => BinaryPrimitives.ReadDoubleBigEndian(Bytes),
                _ => throw new InvalidCastException("Cant convert column type"),
            };
        }

        public string? ToText()
        {
            return Type switch
            {
                SerialType.Null => null,
                SerialType.Text => Encoding.UTF8.GetString(Bytes),
                _ => throw new InvalidCastException("Cant convert column type"),
            };
        }

        public byte[] ToBlob()
        {
            return Type switch
            {
                SerialType.Null => Array.Empty<byte>(),
                SerialType.Blob => Bytes,
                _ => throw new InvalidCastException("Cant convert column type"),
            };
        }

        public string DbgEvaluate()
        {
            return Type switch
            {
                SerialType.Blob => $"Type=Blob: {BitConverter.ToString(Bytes)}",
                SerialType.Text => $"Type=Text: {ToText()}",
                SerialType.Null => "Type=Null",
                SerialType.One => "Type=One",
                SerialType.Zero => "Type=Zero",
                SerialType.Float64 => $"Type=Float: {ToFloat()}",
                _ => $"Type={Type}: {ToInt()} "
            };
        }
    }
