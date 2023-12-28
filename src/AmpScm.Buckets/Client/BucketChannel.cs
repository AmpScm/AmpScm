﻿using System;
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

        internal void Release(bool readOneEol)
        {
            ReadOneEol = readOneEol;
            Client.Release(this);
        }

        internal bool ReadOneEol { get; set; }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Reader.Dispose();
                }

                disposedValue = true;
            }
        }

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
