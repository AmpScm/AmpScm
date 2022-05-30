using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AmpScm.Buckets;

namespace AmpScm.Buckets.Git
{
    [DebuggerDisplay("{GitType}, Version={Version}, ObjectCount={ObjectCount}")]
    public class GitPackHeaderBucket : GitBucket
    {
        BucketStructCollector<GitPackHeader> _header = new BucketStructCollector<GitPackHeader>();

        public GitPackHeaderBucket(Bucket inner) : base(inner)
        {
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            if (!_header.HasResult)
            {
                await _header.ReadAsync(Inner).ConfigureAwait(false);

                // Can fall through for EOF in OK and error case
            }

            return BucketBytes.Eof;
        }

        public string? GitType => _header.HasResult ? new string(_header.Result?.GitType!) : null;
        public int? Version => _header.Result?.Version;
        public long? ObjectCount => _header.Result?.ObjectCount;


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        struct GitPackHeader
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public char[] GitType;
            [NetworkOrder]
            public int Version;
            [NetworkOrder]
            public uint ObjectCount;
        }
    }
}
