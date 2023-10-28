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

var databaseFile = File.OpenRead(path);

// Parse command and act accordingly
if (command == ".dbinfo")
{

    var sql = new SQLite(databaseFile);
    sql.ReadHeader();

    // // reading one page size at a time:
    // byte[] data = new byte[pageSize];
    // databaseFile.Read(data, 0, pageSize);
    // Console.WriteLine(BitConverter.ToString(data));
    
    int tablesNumber = 0;
    Console.WriteLine($"number of tables: {tablesNumber}");
}
else
{
    throw new InvalidOperationException($"Invalid command: {command}");
}

databaseFile.Close();

class SQLite
{
    private readonly FileStream _file;
    private ushort _dbPageSize;
    private uint _dbSizeInPages;
    private uint _pageNrFirstFreelist;
    private uint _nrFreelistPages;


    public SQLite(FileStream file)
    {
        _file = file;
    }

    public void ReadHeader()
    {
        // the first 100 bytes of the database file store info about the database:
        //          Offset	Size	Description
        //          0	16	The header string: "SQLite format 3\000"
        //          16	2	The database page size in bytes. Must be a power of two
        //                          between 512 and 32768 inclusive, or the value 1 representing a page size of 65536.
        //          18	1	File format write version. 1 for legacy; 2 for WAL.
        //          19	1	File format read version. 1 for legacy; 2 for WAL.
        //          20	1	Bytes of unused "reserved" space at the end of each page. Usually 0.
        //          21	1	Maximum embedded payload fraction. Must be 64.
        //          22	1	Minimum embedded payload fraction. Must be 32.
        //          23	1	Leaf payload fraction. Must be 32.
        //          24	4	File change counter.
        //          28	4	Size of the database file in pages. The "in-header database size".
        //          32	4	Page number of the first freelist trunk page.
        //          36	4	Total number of freelist pages.
        //          40	4	The schema cookie.
        //          44	4	The schema format number. Supported schema formats are 1, 2, 3, and 4.
        //          48	4	Default page cache size.
        //          52	4	The page number of the largest root b-tree page when
        //                          in auto-vacuum or incremental-vacuum modes, or zero otherwise.
        //          56	4	The database text encoding. A value of 1 means UTF-8.
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
        if (Encoding.ASCII.GetString(dbInfoBytes[0..15]).Normalize() != "SQLite format 3")
            throw new InvalidDataException("Expect: SQLite format 3\\000");

        // 16	2	The database page size in bytes.
        // Must be a power of two between 512 and 32768 inclusive, or the value 1 representing a page size of 65536.
        _dbPageSize = ReadUInt16BigEndian(dbInfoBytes[16..(16 + 2)]);
        Console.WriteLine($"database page size: {_dbPageSize}");

        // some known values:
        Debug.Assert(dbInfoBytes[21] == 64, "Maximum embedded payload fraction. Must be 64.");
        Debug.Assert(dbInfoBytes[22] == 32, "Minimum embedded payload fraction. Must be 32.");
        Debug.Assert(dbInfoBytes[23] == 32, "Leaf payload fraction. Must be 32.");

        // 28	4	Size of the database file in pages. The "in-header database size".
        _dbSizeInPages = ReadUInt32BigEndian(dbInfoBytes[28..(28 + 4)]);
        Console.WriteLine($"Size in Pages (the in header database) {_dbSizeInPages}");

        // 32	4	Page number of the first freelist trunk page.
        _pageNrFirstFreelist = ReadUInt32BigEndian(dbInfoBytes[32..(32 + 4)]);
        Console.WriteLine($"Page number of the first freelist trunk page: {_pageNrFirstFreelist}");

        // 36	4	Total number of freelist pages.
        _nrFreelistPages = ReadUInt32BigEndian(dbInfoBytes[36..(36 + 4)]);
        Console.WriteLine($"total number of freelist pages: {_nrFreelistPages}");

        // 44	4	The schema format number. Supported schema formats are 1, 2, 3, and 4.
        var schemaFormatNr = ReadUInt32BigEndian(dbInfoBytes[44..(44 + 4)]);
        Console.WriteLine($"schema format number: {schemaFormatNr}");
        Debug.Assert(new uint[] { 1, 2, 3, 4 }.Contains(schemaFormatNr), "Expect schema format number 1, 2, 3 or 4");
        
        // 72	20	Reserved for expansion. Must be zero.
        for (var i=0; i< 20; i++)
            Debug.Assert(dbInfoBytes[i+72] == 0, $"idx={i}. Reserved for expansion. Must be zero");
    }
}