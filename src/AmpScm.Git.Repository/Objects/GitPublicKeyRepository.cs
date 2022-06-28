using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AmpScm.Buckets.Signatures;

namespace AmpScm.Git.Objects
{
    public class GitPublicKeyRepository : IDisposable
    {
        private bool disposedValue;
        public GitRepository Repository { get; }
        readonly Dictionary<IReadOnlyList<byte>, (SignatureBucketKey Key, string principal)> _keys = new(new SigComparer());
        DateTime? _keysRead;

        internal GitPublicKeyRepository(GitRepository repository)
        {
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~GitPublicKeyRepository()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        internal async ValueTask<GitPublicKey?> GetKeyAsync(ReadOnlyMemory<byte> fingerprint)
        {
            var fpa = fingerprint.ToArray();

            if (_keys.TryGetValue(fpa, out var k))
                return new GitPublicKey(k.Key);

            if (!_keysRead.HasValue && (await Repository.Configuration.GetPathAsync("gpg.ssh", "allowedsignersfile").ConfigureAwait(false)) is string cfg
                && File.Exists(cfg))
            {
                DateTime now = DateTime.Now;
                foreach(var l in File.ReadLines(cfg))
                {
                    if (string.IsNullOrWhiteSpace(l))
                        continue;

                    var line = l.Trim();

                    if (line[0] == ';' || line[0] == '#')
                        continue;

                    var p = line.Split(new char[] { ' ' }, 2);

                    if (p.Length != 2)
                        continue;

                    if (GitPublicKey.TryParse(p[1], out var vk))
                    {
                        SignatureBucketKey pk = vk;

                        _keys[pk.Fingerprint] = (vk, p[0]);
                    }
                }
                _keysRead = now;
            }

            if (_keys.TryGetValue(fpa, out k))
                return new GitPublicKey(k.Key);

            return default;
        }

        sealed class SigComparer : IEqualityComparer<IReadOnlyList<byte>>
        {
            public bool Equals(IReadOnlyList<byte>? x, IReadOnlyList<byte>? y)
            {
                return x?.SequenceEqual(y ?? Enumerable.Empty<byte>()) ?? (x is null == y is null);
            }

            public int GetHashCode(IReadOnlyList<byte> obj)
            {
                if (obj.Count >= 4)
                {
#if NET6_0_OR_GREATER
                    return HashCode.Combine(obj[0], obj[1], obj[2], obj[3], obj.Count);
#else
                    return BitConverter.ToInt32(obj.Take(4).ToArray(), 00);
#endif
                }
                else if (obj.Count >= 2)
                    return obj.Count ^ (obj[0] | obj[0] << 8);
                else
                    return obj.Count;
            }
        }
    }
}
