using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace AmpScm.Buckets;

[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
[DebuggerTypeProxy(typeof(DebuggerProxy))]
public partial struct ByteCollector
{

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay
    {
        get
        {
            if (Length == 0)
                return "<Empty>";
            else
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat(CultureInfo.InvariantCulture, "Length={0}, Data=", Length);

                sb.Append(GetBytesForDebugger().AsMemory().AsDebuggerDisplay());
                return sb.ToString();
            }
        }
    }

    private byte[] GetBytesForDebugger()
    {
        // Don't touch actual data!
        if (Length == 0)
            return Array.Empty<byte>();
        else if (_expected > 0)
        {
            return (_bytes as byte[]).AsMemory(0, Length).ToArray();
        }
        else
            return (_bytes as byte[]) ?? _bytes!.ToArray();
    }

    private sealed class DebuggerProxy
    {
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public byte[] Bytes { get; }

        public DebuggerProxy(ByteCollector bytes)
        {
            Bytes = bytes.GetBytesForDebugger();
        }
    }
}
