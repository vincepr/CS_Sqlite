public static class Varint
{
    /// <summary>
    ///     Reads a variable-length integer. Resolves static Huffman encoding of a 64bit int
    ///     that is optimized for positive low val numbers.
    /// </summary>
    /// <param name="bytes">the raw bytes were parsing this from</param>
    /// <param name="idx">idx to the start byte</param>
    /// <returns>the 64bit value encoded AND count of the consumed bytes (1-9 possible)</returns>
    public static (Int64 encodedNr, byte consumedBytes) Read(byte[] bytes, int idx)
#pragma warning disable CS0675 // Bitwise-or operator used on a sign-extended operand
    {
        // most recent byte chunk gets added at the right(so lowest 2^x)
        Int64 nr = 0;
        for (var i = 0; i < 8; i++)
        {
            var cutoff = bytes[idx + i] & 0b0111_1111;
            nr = (nr << 7) | cutoff;
            if ((bytes[idx + i] & 0b1000_0000) == 0)
                return (nr, (byte)(i + 1));
        }

        nr = (nr << 8) | bytes[idx]; // 9th byte goes in fully
        return (nr, 9);
#pragma warning restore CS0675 // Bitwise-or operator used on a sign-extended operand
    }
}