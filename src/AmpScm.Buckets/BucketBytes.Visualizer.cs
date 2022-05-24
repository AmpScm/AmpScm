using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets
{
    [DebuggerTypeProxy(typeof(DebuggerProxy))]
    public partial struct BucketBytes
    {
        sealed class DebuggerProxy
        {
            BucketBytes Bytes { get; }

            public DebuggerProxy(BucketBytes bytes)
            {
                Bytes = bytes;
            }


            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public KeyValuePair<int, ByteDump>[] Items => Enumerable.Range(0, (Bytes.Length + 16 - 1) / 16).Select(
                    (x, n) => new KeyValuePair<int, ByteDump>(x*16, new ByteDump(Bytes.Slice(n * 16, Math.Min(16, Bytes.Length - n * 16))))).ToArray();

            [DebuggerDisplay($"{{{nameof(DisplayValue)},nq}}")]
            public sealed class ByteDump
            {
                ReadOnlyMemory<byte> _bytes;

                public ByteDump(ReadOnlyMemory<byte> bytes)
                {
                    _bytes = bytes;
                }

                public string DisplayValue
                {
                    get
                    {
                        StringBuilder sb = new StringBuilder(100);

                        var span = _bytes.Span;
                        for(int i = 0; i < 16; i++)
                        {
                            if (i < _bytes.Length)
                                sb.Append(span[i].ToString("X2", CultureInfo.InvariantCulture));
                            else
                                sb.Append("  ");

                            sb.Append(' ');
                        }

                        sb.Append("\t\"");
                        for(int i = 0; i < _bytes.Length; i++)
                        {
                            char c = (char)span[i];

                            if (char.IsWhiteSpace(c))
                                sb.Append(' ');
                            else if (char.IsControl(c))
                                sb.Append('.');
                            else
                                sb.Append(c);
                        }
                        sb.Append('\"');
                        return sb.ToString();
                    }
                }
            }
        }
    }
}
