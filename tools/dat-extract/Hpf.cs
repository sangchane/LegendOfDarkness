namespace Lod.DatExtract;

/// <summary>
/// Undoes the compression on a <c>55 AA 02 FF</c> sprite blob. The scheme is the adaptive splay-tree
/// Huffman coder from David Jones' 1988 CACM paper: the tree carries no header, both sides rebuild it as
/// symbols arrive, and each decoded leaf is rotated toward the root so common bytes get shorter codes.
/// </summary>
internal static class Hpf
{
    private static readonly byte[] Magic = [0x55, 0xAA, 0x02, 0xFF];

    private const int MaxChar = 256;
    private const int TwiceMax = 513;
    private const int Root = 1;
    private const int EndOfStream = 256;

    public static bool LooksCompressed(ReadOnlySpan<byte> blob) =>
        blob.Length >= 4 && blob[..4].SequenceEqual(Magic);

    public static byte[] Decompress(byte[] blob)
    {
        if (!LooksCompressed(blob))
        {
            throw new InvalidDataException($"55 AA 02 FF 로 시작하지 않습니다: {Convert.ToHexString(blob.AsSpan(0, Math.Min(4, blob.Length)))}");
        }

        SplayTree tree = new();
        BitReader bits = new(blob, start: Magic.Length);
        List<byte> output = [];

        while (true)
        {
            int node = Root;

            while (node <= MaxChar)
            {
                int bit = bits.Read();

                if (bit < 0)
                {
                    throw new InvalidDataException("끝 표시가 나오기 전에 압축 자료가 떨어졌습니다.");
                }

                node = bit == 0 ? tree.Left(node) : tree.Right(node);
            }

            int symbol = node - (MaxChar + 1);

            if (symbol == EndOfStream)
            {
                return [.. output];
            }

            output.Add((byte)symbol);
            tree.Splay(node);
        }
    }

    /// <summary>Bits arrive least significant first within each byte.</summary>
    private sealed class BitReader(byte[] data, int start)
    {
        private int _position = start;
        private int _current;
        private int _bit = 8;

        public int Read()
        {
            if (_bit >= 8)
            {
                if (_position >= data.Length)
                {
                    return -1;
                }

                _current = data[_position++];
                _bit = 0;
            }

            return (_current >> _bit++) & 1;
        }
    }

    private sealed class SplayTree
    {
        private readonly int[] _left = new int[MaxChar + 2];
        private readonly int[] _right = new int[MaxChar + 2];
        private readonly int[] _up = new int[TwiceMax + 2];

        public SplayTree()
        {
            for (int node = 2; node <= TwiceMax; node++)
            {
                _up[node] = node / 2;
            }

            for (int node = 1; node <= MaxChar; node++)
            {
                _left[node] = 2 * node;
                _right[node] = (2 * node) + 1;
            }
        }

        public int Left(int node) => _left[node];

        public int Right(int node) => _right[node];

        /// <summary>Semi-splay: lift the decoded leaf two levels at a time until it reaches the root.</summary>
        public void Splay(int leaf)
        {
            int node = leaf;

            while (true)
            {
                int parent = _up[node];

                if (parent != Root)
                {
                    int grand = _up[parent];
                    int sibling = _left[grand];

                    if (parent == sibling)
                    {
                        sibling = _right[grand];
                        _right[grand] = node;
                    }
                    else
                    {
                        _left[grand] = node;
                    }

                    if (node == _left[parent])
                    {
                        _left[parent] = sibling;
                    }
                    else
                    {
                        _right[parent] = sibling;
                    }

                    _up[sibling] = parent;
                    _up[node] = grand;
                    node = grand;
                }
                else
                {
                    node = parent;
                }

                if (node == Root)
                {
                    return;
                }
            }
        }
    }
}
