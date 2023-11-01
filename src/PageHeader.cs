using System.Buffers.Binary;

public record PageHeader(
    PageType Type, 
    UInt16 FreeBlockStart,
    UInt16 NumberCells,
    UInt16 StartCells,
    UInt16 FreeBytesNr,
    UInt32? RightMostPointer
)
{
    public static (PageHeader header, byte consumedBytes) Read(byte[] bytes)
    {
        var pageType = bytes[0] switch
        {
            2 => PageType.InteriorIndex,
            5 => PageType.InteriorTable,
            10 => PageType.LeafIndex,
            13 => PageType.LeafTable,
            var b => throw new InvalidDataException($"Illegal Page Type Number: {b}")
        };
        UInt16 freeBlockStart = BinaryPrimitives.ReadUInt16BigEndian(bytes[1..3]);
        UInt16 numberCells = BinaryPrimitives.ReadUInt16BigEndian(bytes[3..5]);
        UInt16 startCells = BinaryPrimitives.ReadUInt16BigEndian(bytes[5..7]);
        if (startCells == 0) startCells = 65535;
        var freeBytesNr = bytes[7];
        UInt32? rightMostPointer = null;
        if (pageType is PageType.InteriorIndex or PageType.InteriorTable)
        {
            rightMostPointer = BinaryPrimitives.ReadUInt32BigEndian(bytes[8..12]);
        }
        return (
            new(pageType, freeBlockStart, numberCells, startCells, freeBytesNr, rightMostPointer),
            pageType is PageType.InteriorIndex or PageType.InteriorTable ? (byte)12 : (byte)8 
        );
    } 
}