using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Client
{
    internal sealed class BucketClientChannel : IDisposable
    {
        private bool disposedValue;

        internal BucketClientChannel(BucketWebClient client, string key, Bucket reader, IBucketWriter writer)
        {
            Client = client;
            Key = key;
            Reader = reader;
            Writer = writer;
        }

        internal string Key { get; }
        internal BucketWebClient Client { get; }
        internal Bucket Reader { get; }
        internal IBucketWriter Writer { get; }

        internal void Release()
        {
            Client.Release(this);
        }

        void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    Reader.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~BucketChannel()
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
    }
}
