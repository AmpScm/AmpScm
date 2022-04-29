using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Client;
using AmpScm.Buckets.Client.Protocols;

namespace AmpScm.Buckets.Client
{
    public abstract class BucketWebRequest
    {
        protected BucketWebClient Client {get; }

        public Uri RequestUri { get; private set;}

        public virtual string? Method
        {
            get => null!;
            set => throw new InvalidOperationException();
        }

        public WebHeaderDictionary Headers { get; } = new WebHeaderDictionary();

        public string? ContentType
        {
            get => Headers[HttpRequestHeader.ContentType];
            set => Headers[HttpRequestHeader.ContentType] = value;
        }

        public long? ContentLength
        {
            get => long.TryParse(Headers[HttpRequestHeader.ContentLength], out var v) && v >= 0 ? v : null;
            set => Headers[HttpRequestHeader.ContentLength] = value?.ToString(CultureInfo.InvariantCulture);
        }

        public bool PreAuthenticate { get; set; }

        public bool FollowRedirects { get; set; } = true;

        protected BucketWebRequest(BucketWebClient client, Uri requestUri)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
            RequestUri = requestUri ?? throw new ArgumentNullException(nameof(requestUri));
        }

        public abstract ValueTask<ResponseBucket> GetResponseAsync();

        public event EventHandler<BasicBucketAuthenticationEventArgs>? BasicAuthentication;

        internal EventHandler<BasicBucketAuthenticationEventArgs>? GetBasicAuthenticationHandlers()
        {
            return BasicAuthentication + Client.GetBasicAuthenticationHandlers();
        }

        internal void UpdateUri(Uri newUri)
        {
            if (newUri == null)
                throw new ArgumentNullException(nameof(newUri));

            RequestUri = newUri;
        }
    }
}
