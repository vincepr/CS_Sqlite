using System.Buffers.Binary;
using System.Collections;
using System.Diagnostics;
using System.Text;
using CS_Sqlite;

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

    private Encoding _encodingType = Encoding.UTF8;
    private ushort _nrOfTables;
    
    /// <summary>
    /// the info stores in the sql_master table. holds info about all other tables in this db
    /// </summary>
    public readonly IEnumerable<Schema> Schemas;

    // public void ReadRawPage()
    // {
    //     // we assume the filestream is always pointing to the start of the page(ex after reading first 100bytes header)
    //     var bytes = new byte[_dbPageSize];
    //     _file.Read()
    // }

    // public void RawReadBytes(byte[] bytes, int offset, int size)
    // {
    //     if (_file.Read(bytes, offset, size) != size)
    //         throw new InvalidDataException("could not read expected size");
    // }

    public SqLite(string path, bool isLogInfo = false)
    {
        _file = File.OpenRead(path);
        // (_dbPageSize, _dbSizeInPages, _encodingType, _nrOfTables ) =  ReadHeader(isLogInfo);
        ReadHeader(isLogInfo);
        Schemas = ReadFirstPage(isLogInfo);
        // _file.Seek(0, SeekOrigin.Begin); // TODO: remove this after testing
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
        // return (_dbPageSize, _dbSizeInPages, _encodingType, _nrOfTables);
    }

    private IEnumerable<Schema> ReadFirstPage(bool isLogInfo = false)
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
        var schemas = cellPointers.Select(cell =>
        {
            var (_length, consumed) = Varint.Read(bytes, cell - 100);
            var (_id, consumed2) = Varint.Read(bytes, cell - 100 + consumed);
            var start = cell - 100 + consumed + consumed2;
            var sqlite_master = Record.Read(bytes[start..]);

            return new Schema(sqlite_master); 
        });
        return schemas;
    }
}