

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
    var sql = new SqLite(path, true);
}
else if (command == ".tables")
{
    var sql = new SqLite(path);
    Console.WriteLine(String.Join(" ",sql.Schemas.Where(el=>el.Name!="sqlite_sequence").Select(el => el.Name)));
}
else
{
    throw new InvalidOperationException($"Invalid command: {command}");
}