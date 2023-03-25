using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Subversion
{
    public class SvnFsFsRevisionBucket : SvnBucket
    {
        readonly Func<long, long, long, ValueTask<Bucket>>? _fetchBase;
        bool _reading;
        bool _readEndRep;
        int _idx;

        public SvnFsFsRevisionBucket(Bucket inner, Func<long, long, long, ValueTask<Bucket>>? fetchBase = null) : base(inner)
        {
            _fetchBase = fetchBase;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = 2146435071)
        {
            while (await ReadRepresentationAsync().ConfigureAwait(false) is (var bk, _))
            {
                BucketBytes bb;

                while (!(bb = await bk.ReadAsync().ConfigureAwait(false)).IsEof)
                {
                    Debug.Write(bb.ToASCIIString());
                }

                await bk.ReadUntilEofAndCloseAsync().ConfigureAwait(false);
            }
            return BucketBytes.Eof;
        }

        private async ValueTask<(Bucket Bucket, int Index)?> ReadRepresentationAsync()
        {
            if (_reading)
                throw new InvalidOperationException();

            while (true)
            {
                var (bb, eol) = await Source.ReadExactlyUntilEolAsync(BucketEol.LF).ConfigureAwait(false);

                if (bb.Length == 7 && bb.StartsWithASCII("ENDREP"))
                {
                    if (!_readEndRep)
                        throw new BucketException();
                    continue;
                }
                else if (bb.Length == 6 && bb.StartsWithASCII("DELTA") && eol == BucketEol.LF)
                {
                    _reading = true;
                    _readEndRep = true;
                    return (new SvnDeltaBucket(Source.NoDispose(), null, () => _reading = false), _idx);
                }
                else if (bb.Length == 6 && bb.StartsWithASCII("PLAIN") && eol == BucketEol.LF)
                {
                    _reading = true;
                    _readEndRep = true;
                    return (new SvnHashBucket(Source.NoDispose(), () => _reading = false), _idx);
                }
                else if (bb.Length > 10 && bb.StartsWithASCII("DELTA "))
                {
                    var parts = bb.Slice(6, eol).Split((byte)' ');

                    long rev = long.Parse(parts[0].ToASCIIString(), CultureInfo.InvariantCulture);
                    long index = long.Parse(parts[1].ToASCIIString(), CultureInfo.InvariantCulture);
                    long baseLength = long.Parse(parts[2].ToASCIIString(), CultureInfo.InvariantCulture);

                    Bucket baseBucket;

                    if (_fetchBase != null)
                        baseBucket = await _fetchBase.Invoke(rev, index, baseLength).ConfigureAwait(false);
                    else
                        baseBucket = Bucket.Create.FromUTF8("This is iota.\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\nThis is more iota\n");

                    _reading = true;
                    _readEndRep = true;
                    return (new SvnDeltaBucket(Source.NoDispose(), baseBucket, () => _reading = false), _idx);
                }
                else if (bb.IndexOf((byte)':') >= 0)
                {
                    _reading = true;
                    return (new SvnKeyValueHeaderBucket(Source.NoDispose(), () => { _reading = false; _idx++; }), _idx);
                }

                break;
            }

            await Source.ReadUntilEofAsync().ConfigureAwait(false);
            return null;
        }
    }
}
