using CS_Sqlite;

/// <summary>
/// https://www.sqlite.org/fileformat.html#record_format
/// </summary>
public record Record(List<Column> Table)
{
    public static Record Read(byte[] bytes)
    {
        var (serialTypes, consumedBytes) = ReadRecordHeader(bytes);
        var table = ReadRecordBody(bytes[consumedBytes..], serialTypes);
        return new Record(table);
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

    private static List<Column> ReadRecordBody(byte[] bytes, List<Int64> serialTypes)
    {
        int consumedBytes = 0;
        List<Column> cols = new();
        foreach (var type in serialTypes)
        {
            var col = Column.ParseColumn(type, bytes[consumedBytes..]);
            consumedBytes += col.Bytes.Length;
            cols.Add(col);
        }

        return cols;
    }
}