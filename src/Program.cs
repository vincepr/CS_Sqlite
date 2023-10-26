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
    databaseFile.Read(pageSizeBytes, 0,2);
    var pageSize = ReadUInt16BigEndian(pageSizeBytes);
    Console.WriteLine($"database page size: {pageSize}");
}
else
{
    throw new InvalidOperationException($"Invalid command: {command}");
}
