

// Parse arguments
var (path, command) = args.Length switch
{
    0 => throw new InvalidOperationException("Missing <database path> and <command>"),
    1 => throw new InvalidOperationException("Missing <command>"),
    _ => (args[0], args[1])
};

switch (command)
{
    case ".dbinfo":
    {
        _ = new SqLite(path, true);
        break;
    }
    case ".tables":
    {
        var sql = new SqLite(path);
        Console.WriteLine(String.Join(" ",sql.Schemas.Where(el=>el.Name!="sqlite_sequence").Select(el => el.Name)));
        break;
    }
    default:
    {
        if (!command.StartsWith(".")) 
            ExecuteSql(path, command);
        else
            throw new InvalidOperationException($"Invalid command: {command}");
        break;
    }
}

static void ExecuteSql(string path, string command)
{
    command = command.ToLower();
    // we just hardcode those patterns for now:
    if (command.StartsWith("select count(*) from") || command.StartsWith("select count (*) from"))
        SqlSelectCountFrom(path, command);
}

// "SELECT COUNT (*) FROM apples")
static void SqlSelectCountFrom(string path, string command)
{
    var cmdTable = command.ToLower().Split(" ").Last();
    var sql = new SqLite(path, true);
    var tables = sql.Schemas.Where(el => el.Name == cmdTable).ToList();
   
    if (tables.Count != 1)
        throw new InvalidDataException("Could not find table or found to many tables with that name");
    
    var table = tables.First();
    var page = sql.ReadPage((int)table.RootPage);
    foreach (var row in page)
    {
        Console.WriteLine(row);
    }

}
