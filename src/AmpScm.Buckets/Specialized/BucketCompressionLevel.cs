using Elskom.Generic.Libs;

namespace AmpScm.Buckets.Specialized
{
    public enum BucketCompressionLevel
    {
        Default = ZlibConst.ZDEFAULTCOMPRESSION,
        Store = ZlibConst.ZNOCOMPRESSION,
        BestSpeed = ZlibConst.ZBESTSPEED,
        Maximum = ZlibConst.ZBESTCOMPRESSION
    }
}
