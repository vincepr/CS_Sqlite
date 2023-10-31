using System.Buffers.Binary;
using System.Collections;
using System.Diagnostics;
using System.Text;

internal class SqLite
{
    private readonly FileStream _file;

    /// <summary>
    ///     all reads and writes from and to the db should be full blocks of these. (so full pages)
    ///     Only the initial 100byte header information is not part of this rule.
    /// </summary>
    private ushort _dbPageSize;

    /// <summary>
    ///     size of the in header database in pages. Might be wrong for db before v3.7.0.
    ///     (Alternatively read actual file size and divide by page-size to get this)
    /// </summary>
    private uint _dbSizeInPages;

    private Encoding _encodingType;
    private ushort _nrOfTables;

    // public void ReadPage()
    // {
    //     // we assume the filestream is always pointing to the start of the page(ex after reading first 100bytes header)
    //     var bytes = new byte[_dbPageSize];
    //     _file.Read()
    // }

    public SqLite(string path, bool isLogInfo = false)
    {
        _file = File.OpenRead(path);
        ReadHeader(isLogInfo);
        ReadFirstPage(isLogInfo);
        // _file.Seek(0, SeekOrigin.Begin); // TODO: remove this after testing
    }

    public void RawReadBytes(byte[] bytes, int offset, int size)
    {
        if (_file.Read(bytes, offset, size) != size)
            throw new InvalidDataException("could not read expected size");
    }


    private byte[] ReadBytes(int size)
    {
        var bytes = new byte[size];
        if (_file.Read(bytes, 0, size) != size)
            throw new InvalidDataException("could not read expected size");
        return bytes;
    }

    // Destructor


    ~SqLite()
    {
        _file.Close();
    }

    private void ReadHeader(bool isLogInfo = false)
    {
        // the first 100 bytes of the database file store info about the database:
        //          Offset	Size	Description
        //          0	16	The header string: "SQLite format 3\000"
        //        ! 16	2	The database page size in bytes. Must be a power of two
        //                          between 512 and 32768 inclusive, or the value 1 representing a page size of 65536.
        //          18	1	File format write version. 1 for legacy; 2 for WAL.
        //          19	1	File format read version. 1 for legacy; 2 for WAL.
        //          20	1	Bytes of unused "reserved" space at the end of each page. Usually 0.
        //          21	1	Maximum embedded payload fraction. Must be 64.
        //          22	1	Minimum embedded payload fraction. Must be 32.
        //          23	1	Leaf payload fraction. Must be 32.
        //          24	4	File change counter.
        //        ! 28	4	Size of the database file in pages. The "in-header database size".
        //          32	4	Page number of the first freelist trunk page.
        //          36	4	Total number of freelist pages.
        //          40	4	The schema cookie.
        //          44	4	The schema format number. Supported schema formats are 1, 2, 3, and 4.
        //          48	4	Default page cache size.
        //          52	4	The page number of the largest root b-tree page when
        //                          in auto-vacuum or incremental-vacuum modes, or zero otherwise.
        //        ! 56	4	The database text encoding. A value of 1 means UTF-8.
        //                          A value of 2 means UTF-16le. A value of 3 means UTF-16be.
        //          60	4	The "user version" as read and set by the user_version pragma.
        //          64	4	True (non-zero) for incremental-vacuum mode. False (zero) otherwise.
        //          68	4	The "Application ID" set by PRAGMA application_id.
        //          72	20	Reserved for expansion. Must be zero.
        //          92	4	The version-valid-for number.
        //          96	4	SQLITE_VERSION_NUMBER

        _file.Seek(0, SeekOrigin.Begin);
        var dbInfoBytes = new byte[100];
        if (_file.Read(dbInfoBytes, 0, 100) != 100)
            throw new InvalidDataException("Expect at least 100 bytes header info at start.");

        // The header string: "SQLite format 3\000": 
        if (Encoding.ASCII.GetString(dbInfoBytes[..16].AsSpan()) != "SQLite format 3\0")
            throw new InvalidDataException("Expect: SQLite format 3\\0");

        // 16	2	The database page size in bytes.
        // Must be a power of two between 512 and 32768 inclusive, or the value 1 representing a page size of 65536.
        _dbPageSize = BinaryPrimitives.ReadUInt16BigEndian(dbInfoBytes[16..(16 + 2)].AsSpan());
        if (isLogInfo) Console.WriteLine($"database page size: {_dbPageSize}");

        // some known values:
        Debug.Assert(dbInfoBytes[21] == 64, "Maximum embedded payload fraction. Must be 64.");
        Debug.Assert(dbInfoBytes[22] == 32, "Minimum embedded payload fraction. Must be 32.");
        Debug.Assert(dbInfoBytes[23] == 32, "Leaf payload fraction. Must be 32.");

        // 28	4	Size of the database file in pages. The "in-header database size".
        _dbSizeInPages = BinaryPrimitives.ReadUInt32BigEndian(dbInfoBytes[28..(28 + 4)].AsSpan());
        if (isLogInfo) Console.WriteLine($"Size in Pages (the in header database) {_dbSizeInPages}");

        // 32	4	Page number of the first freelist trunk page.
        var pageNrFirstFreelist = BinaryPrimitives.ReadUInt32BigEndian(dbInfoBytes[32..(32 + 4)].AsSpan());
        if (isLogInfo) Console.WriteLine($"Page number of the first freelist trunk page: {pageNrFirstFreelist}");

        // 36	4	Total number of freelist pages.
        var nrFreelistPages = BinaryPrimitives.ReadUInt32BigEndian(dbInfoBytes[36..(36 + 4)].AsSpan());
        if (isLogInfo) Console.WriteLine($"total number of freelist pages: {nrFreelistPages}");

        // 44	4	The schema format number. Supported schema formats are 1, 2, 3, and 4.
        var schemaFormatNr = BinaryPrimitives.ReadUInt32BigEndian(dbInfoBytes[44..(44 + 4)].AsSpan());
        if (isLogInfo) Console.WriteLine($"schema format number: {schemaFormatNr}");
        Debug.Assert(new uint[] { 1, 2, 3, 4 }.Contains(schemaFormatNr), "Expect schema format number 1, 2, 3 or 4");

        // 56	4	The database text encoding. A value of 1 means UTF-8.
        var encodingType = BinaryPrimitives.ReadUInt32BigEndian(dbInfoBytes[56..(56 + 4)].AsSpan());
        if (isLogInfo)
            Console.WriteLine($"database text encoding: {encodingType}. With 1->UTF-8 || 2->UTF16LE || 3->UTF16BE");
        _encodingType = encodingType switch
        {
            1 => Encoding.UTF8,
            2 => Encoding.Unicode, // utf-16 little endian byte order.
            3 => Encoding.BigEndianUnicode, // utf-16 big endian byte order.
            _ => throw new NotSupportedException(
                $"NOT supported database text encoding: {encodingType}. \n" +
                $"Expected on of the following: | 1->UTF-8 || 2->UTF16LE || 3->UTF16BE")
        };

        // 72	20	Reserved for expansion. Must be zero.
        for (var i = 0; i < 20; i++)
            Debug.Assert(dbInfoBytes[i + 72] == 0, $"idx={i}. Reserved for expansion. Must be zero");
    }

    private void ReadFirstPage(bool isLogInfo = false)
    {
        // this directly follows the 100byte header. So it must be called after reading those bytes or Seek(100)
        // stores info about all tables in this file.
        // - aka the sqlite schema

        var bytes = ReadBytes(_dbPageSize - 100);

        // we expect this to be a leaf table btree-page. Nothing must be at the start?
        if (bytes[0] != 13)
            throw new InvalidDataException("Type of first page MUST be a leaf table btree-page. val=13");

        // if (bytes[0] == 2) Console.WriteLine("type: interior index btree-page"); 
        // else if (bytes[0] == 5) Console.WriteLine("type: interior table btree-page"); 
        // else if (bytes[0] == 10) Console.WriteLine("type: leaf index btree-page"); 
        // else if (bytes[0] == 13) Console.WriteLine("type: leaf table btree-page");
        // else throw new InvalidDataException("Illegal flag for b-tree page type.");

        // we know every cell in this first table will represent a db-table so:
        _nrOfTables = BinaryPrimitives.ReadUInt16BigEndian(bytes[3..(3 + 2)]);
        if (isLogInfo) Console.WriteLine($"Number of tables: {_nrOfTables}");

        // obtain all cell pointers (2byte each)
        var cellPointers = bytes[8..]
            .Chunk(2)
            .Take(_nrOfTables)
            .Select(u => BinaryPrimitives.ReadUInt16BigEndian(u));

        foreach (var cell in cellPointers)
        {
            var (_length, consumed) = Varint.Read(bytes, cell - 100);
            var (_id, consumed2) = Varint.Read(bytes, cell - 100 + consumed);
            var start = cell - 100 + consumed + consumed2;
            var record = Record.Read(bytes[start..]);
        }
    }

}


/// <summary>
/// https://www.sqlite.org/fileformat.html#record_format
/// </summary>
public record Record
{
    
    public static Record Read(byte[] bytes)
    {
        var (serialTypes, consumedBytes) = ReadRecordHeader(bytes);
        Console.WriteLine($"length of serialTypes: {serialTypes.Count}");
        ReadRecordBody(bytes[consumedBytes..], serialTypes);
        
        return new Record();
    }

    /// <summary>
    /// returns a list of Serial Types after parsing the header
    /// </summary>
    /// <param name="bytes"></param>
    private static (List<Int64> serialTypes, byte consumedBytes) ReadRecordHeader(byte[] bytes)
    {
        List<Int64> serialTypes = new List<Int64>();
        
        var (totalCountHeaderBytes, consumedBytes) = Varint.Read(bytes, 0);
        do
        {
            // one of these per column, these Types define the datatype of each colum.
            var (serialType, moreConsumedBytes) = Varint.Read(bytes, consumedBytes);
            consumedBytes += moreConsumedBytes;
            serialTypes.Add(serialType);

        } while (consumedBytes < totalCountHeaderBytes);

        return (serialTypes, consumedBytes);
    }

    private static void ReadRecordBody(byte[] bytes, List<Int64> serialTypes)
    {
        foreach (var type in serialTypes)
        {
            Console.WriteLine($"type: {type}");
            var col = Column.ParseColumn(type, bytes);
            Console.WriteLine(col.DbgEvaluate());
        }
    }

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

    public record Column(SerialType Type, byte[] Bytes)
    {
        public static Column ParseColumn(Int64 serialType, byte[] bytes)
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
                var odd when odd%2==1 => new(SerialType.Text, bytes[..GetLenBlob(odd)]),
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
}

public class Varint
{
    /// <summary>
    ///     Reads a variable-length integer. Resolves static Huffman encoding of a 64bit int
    ///     that is optimized for positive low val numbers.
    /// </summary>
    /// <param name="bytes">the raw bytes were parsing this from</param>
    /// <param name="idx">idx to the start byte</param>
    /// <returns>the 64bit value encoded AND count of the consumed bytes (1-9 possible)</returns>
    public static (Int64 encodedNr, byte consumedBytes) Read(byte[] bytes, int idx)
#pragma warning disable CS0675 // Bitwise-or operator used on a sign-extended operand
    {
        // most recent byte chunk gets added at the right(so lowest 2^x)
        Int64 nr = 0;
        for(var i=0; i<8; i++)
        {
            var cutoff = bytes[idx + i] & 0b0111_1111;
            nr = (nr << 7) | cutoff;
            if ((bytes[idx + i] & 0b1000_0000) == 0)
                return (nr, (byte)(i+1));
        }

        nr = (nr << 8) | bytes[idx];    // 9th byte goes in fully
        return (nr, 9);
#pragma warning restore CS0675 // Bitwise-or operator used on a sign-extended operand
    } 
}
