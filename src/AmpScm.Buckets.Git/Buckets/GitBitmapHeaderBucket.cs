using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Git
{
    [DebuggerDisplay("{BitmapType}, Version={Version}, Flags={Flags}, ObjectCount={ObjectCount}")]
    public class GitBitmapHeaderBucket : GitBucket
    {
        BucketStructCollector<GitPackHeader> _header = new BucketStructCollector<GitPackHeader>();

        public GitBitmapHeaderBucket(Bucket inner) : base(inner)
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

        public string? BitmapType => _header.HasResult ? new string(_header.Result?.BitmapType!) : null;
        public int? Version => _header.Result?.Version;
        public int? Flags => _header.Result?.Flags;
        public int? ObjectCount => _header.Result?.ObjectCount;


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        struct GitPackHeader
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public char[] BitmapType;
            [NetworkOrder]
            public short Version;
            [NetworkOrder]
            public short Flags;
            [NetworkOrder]
            public int ObjectCount;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] Checksum;
        }
    }
}
