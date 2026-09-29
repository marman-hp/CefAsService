using System;
using System.IO;

namespace Xilium.CefGlue.Broker.Admin
{
    internal static class Bzip2Decoder
    {
        private const int MaxCodeLength = 20;
        private const int GroupSize = 50;

        public static byte[] Decompress(byte[] input)
        {
            var bits = new BitReader(input);
            var output = new MemoryStream();

            do
            {
                if (bits.Read(8) != 'B' || bits.Read(8) != 'Z' || bits.Read(8) != 'h')
                {
                    throw new InvalidDataException("Not a bzip2 stream.");
                }

                var level = bits.Read(8) - '0';
                if (level < 1 || level > 9)
                {
                    throw new InvalidDataException("Bad bzip2 block size.");
                }

                var tt = new int[level * 100_000];
                while (true)
                {
                    var magicHigh = bits.Read(24);
                    var magicLow = bits.Read(24);
                    if (magicHigh == 0x177245 && magicLow == 0x385090)
                    {
                        bits.Read(32);
                        bits.AlignToByte();
                        break;
                    }

                    if (magicHigh != 0x314159 || magicLow != 0x265359)
                    {
                        throw new InvalidDataException("Bad bzip2 block header.");
                    }

                    DecodeBlock(bits, tt, output);
                }
            }
            while (bits.BytesRemaining >= 4 && input[bits.BytePosition] == 'B' && input[bits.BytePosition + 1] == 'Z');

            return output.ToArray();
        }

        private static void DecodeBlock(BitReader bits, int[] tt, MemoryStream output)
        {
            bits.Read(32);
            if (bits.Read(1) != 0)
            {
                throw new InvalidDataException("Randomised bzip2 blocks (bzip2 0.9.0 era) are not supported.");
            }

            var origPtr = bits.Read(24);

            var seqToUnseq = new byte[256];
            var inUse = 0;
            var groupsUsed = bits.Read(16);
            for (var i = 0; i < 16; i++)
            {
                if ((groupsUsed & (0x8000 >> i)) == 0)
                {
                    continue;
                }

                var groupBits = bits.Read(16);
                for (var j = 0; j < 16; j++)
                {
                    if ((groupBits & (0x8000 >> j)) != 0)
                    {
                        seqToUnseq[inUse++] = (byte)(i * 16 + j);
                    }
                }
            }

            if (inUse == 0)
            {
                throw new InvalidDataException("Empty bzip2 symbol map.");
            }

            var alphaSize = inUse + 2;
            var groupCount = bits.Read(3);
            var selectorCount = bits.Read(15);
            if (groupCount < 2 || groupCount > 6 || selectorCount < 1)
            {
                throw new InvalidDataException("Bad bzip2 Huffman group header.");
            }

            var selectorMtf = new byte[groupCount];
            for (var i = 0; i < groupCount; i++)
            {
                selectorMtf[i] = (byte)i;
            }

            var selectors = new byte[selectorCount];
            for (var i = 0; i < selectorCount; i++)
            {
                var j = 0;
                while (bits.Read(1) == 1)
                {
                    if (++j >= groupCount)
                    {
                        throw new InvalidDataException("Bad bzip2 selector.");
                    }
                }

                var value = selectorMtf[j];
                Array.Copy(selectorMtf, 0, selectorMtf, 1, j);
                selectorMtf[0] = value;
                selectors[i] = value;
            }

            var tables = new HuffmanTable[groupCount];
            var lengths = new int[alphaSize];
            for (var t = 0; t < groupCount; t++)
            {
                var current = bits.Read(5);
                for (var s = 0; s < alphaSize; s++)
                {
                    while (true)
                    {
                        if (current < 1 || current > MaxCodeLength)
                        {
                            throw new InvalidDataException("Bad bzip2 code length.");
                        }

                        if (bits.Read(1) == 0)
                        {
                            break;
                        }

                        current += bits.Read(1) == 0 ? 1 : -1;
                    }

                    lengths[s] = current;
                }

                tables[t] = new HuffmanTable(lengths, alphaSize);
            }

            var mtf = new byte[256];
            for (var i = 0; i < 256; i++)
            {
                mtf[i] = (byte)i;
            }

            var counts = new int[256];
            var endOfBlock = inUse + 1;
            var length = 0;
            var selectorIndex = 0;
            var groupLeft = 0;
            HuffmanTable table = null;
            var run = 0;
            var runWeight = 1;

            while (true)
            {
                if (groupLeft == 0)
                {
                    if (selectorIndex >= selectorCount)
                    {
                        throw new InvalidDataException("Ran out of bzip2 selectors.");
                    }

                    table = tables[selectors[selectorIndex++]];
                    groupLeft = GroupSize;
                }

                groupLeft--;
                var symbol = table.Decode(bits);

                if (symbol <= 1)
                {
                    run += (symbol + 1) * runWeight;
                    runWeight <<= 1;
                    if (run > tt.Length)
                    {
                        throw new InvalidDataException("bzip2 run overflows the block.");
                    }

                    continue;
                }

                if (run > 0)
                {
                    var runByte = seqToUnseq[mtf[0]];
                    if (length + run > tt.Length)
                    {
                        throw new InvalidDataException("bzip2 block overflow.");
                    }

                    counts[runByte] += run;
                    while (run-- > 0)
                    {
                        tt[length++] = runByte;
                    }

                    run = 0;
                    runWeight = 1;
                }

                if (symbol == endOfBlock)
                {
                    break;
                }

                if (length >= tt.Length)
                {
                    throw new InvalidDataException("bzip2 block overflow.");
                }

                var position = symbol - 1;
                var mtfValue = mtf[position];
                Array.Copy(mtf, 0, mtf, 1, position);
                mtf[0] = mtfValue;
                var value = seqToUnseq[mtfValue];
                counts[value]++;
                tt[length++] = value;
            }

            if (origPtr >= length)
            {
                throw new InvalidDataException("Bad bzip2 origin pointer.");
            }

            var start = new int[256];
            for (int i = 0, sum = 0; i < 256; i++)
            {
                start[i] = sum;
                sum += counts[i];
            }

            for (var i = 0; i < length; i++)
            {
                var b = tt[i] & 0xFF;
                tt[start[b]++] |= i << 8;
            }

            var pos = tt[origPtr] >> 8;
            var last = -1;
            var same = 0;
            for (var i = 0; i < length; i++)
            {
                var entry = tt[pos];
                var b = entry & 0xFF;
                pos = entry >> 8;

                if (same == 4)
                {
                    for (var r = 0; r < b; r++)
                    {
                        output.WriteByte((byte)last);
                    }

                    same = 0;
                    last = -1;
                    continue;
                }

                output.WriteByte((byte)b);
                same = b == last ? same + 1 : 1;
                last = b;
            }
        }

        private sealed class HuffmanTable
        {
            private readonly int[] _countPerLength = new int[MaxCodeLength + 1];
            private readonly int[] _symbols;

            public HuffmanTable(int[] lengths, int alphaSize)
            {
                _symbols = new int[alphaSize];
                for (var s = 0; s < alphaSize; s++)
                {
                    _countPerLength[lengths[s]]++;
                }

                var offsets = new int[MaxCodeLength + 2];
                for (var len = 1; len <= MaxCodeLength; len++)
                {
                    offsets[len + 1] = offsets[len] + _countPerLength[len];
                }

                for (var s = 0; s < alphaSize; s++)
                {
                    _symbols[offsets[lengths[s]]++] = s;
                }
            }

            public int Decode(BitReader bits)
            {
                int code = 0, first = 0, index = 0;
                for (var len = 1; len <= MaxCodeLength; len++)
                {
                    code |= bits.Read(1);
                    var count = _countPerLength[len];
                    if (code - first < count)
                    {
                        return _symbols[index + code - first];
                    }

                    index += count;
                    first = (first + count) << 1;
                    code <<= 1;
                }

                throw new InvalidDataException("Bad bzip2 Huffman code.");
            }
        }

        private sealed class BitReader
        {
            private readonly byte[] _data;
            private int _position;
            private int _bitBuffer;
            private int _bitCount;

            public BitReader(byte[] data) => _data = data;

            public int BytePosition => _position;
            public int BytesRemaining => _data.Length - _position;

            public int Read(int count)
            {
                if (count > 24)
                {
                    return (Read(count - 16) << 16) | Read(16);
                }

                while (_bitCount < count)
                {
                    if (_position >= _data.Length)
                    {
                        throw new InvalidDataException("Unexpected end of bzip2 data.");
                    }

                    _bitBuffer = (_bitBuffer << 8) | _data[_position++];
                    _bitCount += 8;
                }

                _bitCount -= count;
                return (_bitBuffer >> _bitCount) & ((1 << count) - 1);
            }

            public void AlignToByte()
            {
                _bitCount -= _bitCount % 8;
                _position -= _bitCount / 8;
                _bitCount = 0;
                _bitBuffer = 0;
            }
        }
    }
}
