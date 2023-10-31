namespace CS_Sqlite;
/// <summary>
/// the first table holds info about structure of the other tables. aka schema, aka squlite_master
/// https://www.sqlite.org/fileformat.html#record_format
/// </summary>
public record Schema
{
    public string Type;
    public string Name;
    public string TableName;
    public Int64 RootPage;
    public string SqlConstructor;

    public Schema(Record record)
    {
        Type = record.Table[0].ToText()!;
        Name = record.Table[1].ToText()!;
        TableName = record.Table[2].ToText()!;
        RootPage = record.Table[3].ToInt();
        SqlConstructor = record.Table[4].ToText()!;
    }
}