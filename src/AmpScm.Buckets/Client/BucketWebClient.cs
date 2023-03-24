using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AmpScm.Buckets.Client.Protocols;

namespace AmpScm.Buckets.Client
{
    public class BucketWebClient : IDisposable
    {
        private bool disposedValue;

        public BucketWebRequest CreateRequest(Uri requestUri)
        {
            if (requestUri == null)
                throw new ArgumentNullException(nameof(requestUri));

            switch (requestUri.Scheme.ToUpperInvariant())
            {
                case "HTTP":
                    return new HttpBucketWebRequest(this, requestUri);
                case "HTTPS":
                    return new HttpsBucketWebRequest(this, requestUri);
                default:
                    throw new NotSupportedException();
            }
        }

        public BucketWebRequest CreateRequest(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return CreateRequest(uri);
            else
                throw new ArgumentOutOfRangeException(url);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    foreach(var c in _channels.Values)
                    {
                        c.Dispose();
                    }
                    _channels.Clear();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        private readonly Dictionary<string, BucketClientChannel> _channels = new Dictionary<string, BucketClientChannel>();

        internal void Release(BucketClientChannel bucketChannel)
        {
            lock (_channels)
            {
                _channels[bucketChannel.Key] = bucketChannel;
            }
        }

        internal bool TryGetChannel(Uri uri, out BucketClientChannel? channel)
        {
            lock (_channels)
            {
                string key = uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);
                if (_channels.TryGetValue(key, out channel))
                {
                    _channels.Remove(key);
                    return true;
                }
            }
            return false;
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~BucketWebClient()
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

        public event EventHandler<BasicBucketAuthenticationEventArgs>? BasicAuthentication;

        internal EventHandler<BasicBucketAuthenticationEventArgs>? GetBasicAuthenticationHandlers()
        {
            return BasicAuthentication;
        }
    }
}
