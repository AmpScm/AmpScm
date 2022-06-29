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
        readonly Dictionary<ReadOnlyMemory<byte>, GitPublicKey> _keys = new(new SigComparer());
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
            fingerprint = GitPublicKey.HashPrint(fingerprint); // Standardize format

            if (_keys.TryGetValue(fingerprint, out var key))
                return key;

            if (!_keysRead.HasValue && (await Repository.Configuration.GetPathAsync("gpg.ssh", "allowedsignersfile").ConfigureAwait(false)) is string cfg
                && File.Exists(cfg))
            {
                DateTime now = DateTime.Now;
                foreach (var l in File.ReadLines(cfg))
                {
                    if (string.IsNullOrWhiteSpace(l))
                        continue;

                    var line = l.Trim();

                    if (line[0] == ';' || line[0] == '#')
                        continue;

                    var p = line.Split(new char[] { ' ' }, 2);

                    if (p.Length != 2)
                        continue;

                    if (GitPublicKey.TryParse(p[1], out var vk, principal: p[0]))
                    {
                        _keys[vk.Fingerprint] = vk;
                    }
                }
                _keysRead = now;
            }

            if (_keys.TryGetValue(fingerprint, out key))
                return key;

            return default;
        }

        sealed class SigComparer : IEqualityComparer<ReadOnlyMemory<byte>>
        {
            public bool Equals(ReadOnlyMemory<byte> x, ReadOnlyMemory<byte> y)
            {
                return x.Span.SequenceEqual(y.Span);
            }

            public int GetHashCode(ReadOnlyMemory<byte> obj)
            {
                if (obj.Length >= 4)
                {
                    var sp = obj.Span;
#if NET6_0_OR_GREATER
                    return HashCode.Combine(sp[0], sp[1], sp[sp.Length / 2], sp[(sp.Length / 2) + 1], sp.Length);
#else
                    return (sp[0] << 24 | sp[1] << 16 | sp[sp.Length / 2] << 8 | sp[(sp.Length / 2) + 1]) ^ sp.Length;
#endif
                }
                else if (obj.Length >= 2)
                    return obj.Length ^ (obj.Span[0] | obj.Span[1] << 8);
                else
                    return obj.Length;
            }
        }
    }
}
