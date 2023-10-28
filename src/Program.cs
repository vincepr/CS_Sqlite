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
    // You can use print statements as follows for debugging, they'll be visible when running tests.
    Console.WriteLine("Logs from your program will appear here!");

    // we throw away first 16 byte header
    byte[] pageSizeBytes = new byte[2];
    databaseFile.Seek(16, SeekOrigin.Begin);

    // the main database consits of one or more pages. The size of a page is power of two between 512 and 65536.
    // - all pages in the same database are the same size.
    databaseFile.Read(pageSizeBytes, 0,2);
    var pageSize = ReadUInt16BigEndian(pageSizeBytes);
    Console.WriteLine($"database page size: {pageSize}");
    
    // // reading one page size at a time:
    // byte[] data = new byte[pageSize];
    // databaseFile.Read(data, 0, pageSize);
    // Console.WriteLine(BitConverter.ToString(data));

    // byte[] data1 = new byte[pageSize];
    // databaseFile.Read(data1, 0, pageSize);
    // Console.WriteLine(BitConverter.ToString(data1));


    int tablesNumber = 0;
    Console.WriteLine($"number of tables: {tablesNumber}");
}
else
{
    throw new InvalidOperationException($"Invalid command: {command}");
}

static void ParseHeader() {
// the first 100 bytes of the database file store info about the database:

//          Offset	Size	Description
//          0	16	The header string: "SQLite format 3\000"
//          16	2	The database page size in bytes. Must be a power of two between 512 and 32768 inclusive, or the value 1 representing a page size of 65536.
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
//          52	4	The page number of the largest root b-tree page when in auto-vacuum or incremental-vacuum modes, or zero otherwise.
//          56	4	The database text encoding. A value of 1 means UTF-8. A value of 2 means UTF-16le. A value of 3 means UTF-16be.
//          60	4	The "user version" as read and set by the user_version pragma.
//          64	4	True (non-zero) for incremental-vacuum mode. False (zero) otherwise.
//          68	4	The "Application ID" set by PRAGMA application_id.
//          72	20	Reserved for expansion. Must be zero.
//          92	4	The version-valid-for number.
//          96	4	SQLITE_VERSION_NUMBER


}
