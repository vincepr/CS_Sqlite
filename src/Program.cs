using System.Diagnostics;
using System.Text;
using static System.Buffers.Binary.BinaryPrimitives;

// Parse arguments
var (path, command) = args.Length switch
{
    0 => throw new InvalidOperationException("Missing <database path> and <command>"),
    1 => throw new InvalidOperationException("Missing <command>"),
    _ => (args[0], args[1])
};


// Parse command and act accordingly
if (command == ".dbinfo")
{
    var sql = new SQLite(path, true);

    // // reading one page size at a time:
    var pagesize = 4096;
    byte[] data = new byte[pagesize];
    sql.ReadBytes(data, 0, pagesize);
    Console.WriteLine(BitConverter.ToString(data));
    
    int tablesNumber = 0;
    Console.WriteLine($"number of tables: {tablesNumber}");
}
else
{
    throw new InvalidOperationException($"Invalid command: {command}");
}

class SQLite
{
    private readonly FileStream _file;
    /// <summary>
    /// all reads and writes from and to the db should be full blocks of these. (so full pages)
    /// Only the initial 100byte header information is not part of this rule.
    /// </summary>
    private ushort _dbPageSize;
    /// <summary>
    /// size of the in header database in pages. Might be wrong for db before v3.7.0.
    /// (Alternatively read actual file size and divide by page-size to get this)
    /// </summary>
    private uint _dbSizeInPages;

    private Encoding _encodingType;

    public void ReadBytes(byte[] bytes, int offset, int size)
    {
        if (_file.Read(bytes, offset, size) != size) 
            throw new InvalidDataException("could not read expected size");

    }

    public SQLite(string path, bool isLogInfo=false)
    {
        _file = File.OpenRead(path);
        ReadHeader(isLogInfo);
    }

    // Destructor


    ~SQLite() 
    {
        _file.Close();
    }

    private void ReadHeader(bool isLogInfo=false)
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
        byte[] dbInfoBytes = new byte[100];
        if (_file.Read(dbInfoBytes, 0, 100) != 100)
            throw new InvalidDataException("Expect at least 100 bytes header info at start.");

        // The header string: "SQLite format 3\000": 
        if (Encoding.ASCII.GetString(dbInfoBytes[0..16]) != "SQLite format 3\0")
            throw new InvalidDataException("Expect: SQLite format 3\\0");

        // 16	2	The database page size in bytes.
        // Must be a power of two between 512 and 32768 inclusive, or the value 1 representing a page size of 65536.
        _dbPageSize = ReadUInt16BigEndian(dbInfoBytes[16..(16 + 2)]);
        if(isLogInfo) Console.WriteLine($"database page size: {_dbPageSize}");

        // some known values:
        Debug.Assert(dbInfoBytes[21] == 64, "Maximum embedded payload fraction. Must be 64.");
        Debug.Assert(dbInfoBytes[22] == 32, "Minimum embedded payload fraction. Must be 32.");
        Debug.Assert(dbInfoBytes[23] == 32, "Leaf payload fraction. Must be 32.");

        // 28	4	Size of the database file in pages. The "in-header database size".
        _dbSizeInPages = ReadUInt32BigEndian(dbInfoBytes[28..(28 + 4)]);
        if(isLogInfo) Console.WriteLine($"Size in Pages (the in header database) {_dbSizeInPages}");

        // 32	4	Page number of the first freelist trunk page.
        var pageNrFirstFreelist = ReadUInt32BigEndian(dbInfoBytes[32..(32 + 4)]);
        if(isLogInfo) Console.WriteLine($"Page number of the first freelist trunk page: {pageNrFirstFreelist}");

        // 36	4	Total number of freelist pages.
        var nrFreelistPages = ReadUInt32BigEndian(dbInfoBytes[36..(36 + 4)]);
        if(isLogInfo) Console.WriteLine($"total number of freelist pages: {nrFreelistPages}");

        // 44	4	The schema format number. Supported schema formats are 1, 2, 3, and 4.
        var schemaFormatNr = ReadUInt32BigEndian(dbInfoBytes[44..(44 + 4)]);
        if(isLogInfo) Console.WriteLine($"schema format number: {schemaFormatNr}");
        Debug.Assert(new uint[] { 1, 2, 3, 4 }.Contains(schemaFormatNr), "Expect schema format number 1, 2, 3 or 4");
        
        // 56	4	The database text encoding. A value of 1 means UTF-8.
        var encodingType = ReadUInt32BigEndian(dbInfoBytes[56..(56 + 4)]);
        if (isLogInfo)
            Console.WriteLine($"database text encoding: {encodingType}. With 1->UTF-8 || 2->UTF16LE || 3->UTF16BE");
        _encodingType = (encodingType)switch
        {
            1 => Encoding.UTF8,
            2 => Encoding.Unicode, // utf-16 little endian byte order.
            3 => Encoding.BigEndianUnicode,  // utf-16 big endian byte order.
            _ => throw new NotSupportedException(
                $"NOT supported database text encoding: {encodingType}. \n" +
                $"Expected on of the following: | 1->UTF-8 || 2->UTF16LE || 3->UTF16BE")
        };
        
        // 72	20	Reserved for expansion. Must be zero.
        for (var i=0; i< 20; i++)
            Debug.Assert(dbInfoBytes[i+72] == 0, $"idx={i}. Reserved for expansion. Must be zero");
    }
}