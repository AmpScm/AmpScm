using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Git
{
    [AttributeUsage(AttributeTargets.Field)]
    sealed class NetworkOrderAttribute : Attribute
    {

    }

    [DebuggerDisplay("Result={Result}")]
    internal class BucketStructCollector<TRead> where TRead : struct
    {
        object? _state;
        int _pos;

        public async ValueTask<ValueOrEof<TRead>> ReadAsync(Bucket b)
        {
            if (_state is TRead r)
                return r;

            byte[] bytes = (byte[])_state!;
            if (bytes == null)
                _state = bytes = new byte[Marshal.SizeOf(typeof(TRead))];

            do
            {
                var rd = await b.ReadAsync(bytes.Length - _pos).ConfigureAwait(false);

                if (rd.Length > 0)
                {
                    rd.CopyTo(new Memory<byte>(bytes, _pos, bytes.Length - _pos));
                    _pos += rd.Length;
                }
                else if (rd.IsEof)
                    return ValueOrEof.Eof;
            }
            while (_pos < bytes.Length);

            var v = LoadStruct(bytes);
            _state = v;

            return v;
        }

        unsafe TRead LoadStruct(byte[] bytes)
        {
            object r;
            fixed (byte* pData = &bytes[0])
            {
                r = Marshal.PtrToStructure<TRead>((IntPtr)pData);
            }

            foreach (var f in typeof(TRead).GetFields())
            {
                if (f.GetCustomAttributes<NetworkOrderAttribute>()?.Any() ?? false)
                {
                    var dd = f.GetValue(r);

                    if (dd is int di)
                        dd = NetBitConverter.FromNetwork(di);
                    else if (dd is long dl)
                        dd = NetBitConverter.FromNetwork(dl);
                    else if (dd is short ds)
                        dd = NetBitConverter.FromNetwork(ds);
                    else if (dd is uint dui)
                        dd = NetBitConverter.FromNetwork(dui);
                    else if (dd is ulong dul)
                        dd = NetBitConverter.FromNetwork(dul);
                    else if (dd is short dus)
                        dd = NetBitConverter.FromNetwork(dus);
                    else
                        continue;

                    f.SetValue(r, dd);
                }
            }
            return (TRead)r;
        }

        public TRead? Result => _state as TRead?;

        public bool HasResult => _state is TRead;
    }
}
