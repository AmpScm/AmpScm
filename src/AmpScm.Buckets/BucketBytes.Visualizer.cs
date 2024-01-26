using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets;

[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
[DebuggerTypeProxy(typeof(DebuggerProxy))]
public partial struct BucketBytes
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay
    {
        get
        {
            if (IsEof)
                return "<EOF>";
            else
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat(CultureInfo.InvariantCulture, "Length={0}, Data=", Length);

                sb.Append(this.AsDebuggerDisplay());
                return sb.ToString();
            }
        }
    }


    private sealed class DebuggerProxy
    {
        private BucketBytes Bytes { get; }

        public DebuggerProxy(BucketBytes bytes)
        {
            Bytes = bytes;
        }


        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public ByteDump[] Items => Enumerable.Range(0, (Bytes.Length + 16 - 1) / 16).Select(
                (x, n) => new ByteDump(Bytes.Slice(n * 16, Math.Min(16, Bytes.Length - n * 16)), n*16)).ToArray();

        [DebuggerDisplay($"{{{nameof(DisplayValue)},nq}}", Name = $"{{{nameof(DisplayKey)},nq}}")]
        public sealed class ByteDump
        {
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private readonly uint _offset;
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private readonly ReadOnlyMemory<byte> _bytes;

            public ByteDump(ReadOnlyMemory<byte> bytes, int offset)
            {
                _offset = (uint)offset;
                _bytes = bytes;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public string DisplayKey
            {
                get
                {
                    StringBuilder sb = new StringBuilder(100);

                    sb.Append(_offset.ToString("X8", CultureInfo.InvariantCulture));
                    sb.Append(" -  ");


                    var span = _bytes.Span;
                    for (int i = 0; i < 16; i++)
                    {
                        if (i < _bytes.Length)
                            sb.Append(span[i].ToString("X2", CultureInfo.InvariantCulture));
                        else
                            sb.Append("  ");

                        sb.Append(' ');
                    }

                    return sb.ToString();
                }
            }

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public string DisplayValue
            {
                get
                {
                    StringBuilder sb = new StringBuilder(100);

                    sb.Append(_offset.ToString("X8", CultureInfo.InvariantCulture));
                    sb.Append("   \"");
                    var span = _bytes.Span;
                    for (int i = 0; i < _bytes.Length; i++)
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
