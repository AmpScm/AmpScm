using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Client.Protocols;
using AmpScm.Buckets.Specialized;
using BasicHandler = System.EventHandler<AmpScm.Buckets.Client.BasicBucketAuthenticationEventArgs>;

namespace AmpScm.Buckets.Client.Http
{
    public class HttpResponseBucket : ResponseBucket
    {
        Bucket? _reader;
        private bool _doneAtEof;
        WebHeaderDictionary? _responseHeaders;
        Action? _succes;
        Action? _authFailed;
        int _nRedirects;
        Stack<Bucket>? _readUntilEof;

        const BucketEol ResponseEol = BucketEol.LF | BucketEol.CRLF;

        public string? HttpVersion { get; private set; }
        public int? HttpStatus { get; private set; }
        public string? HttpMessage { get; private set; }

        internal HttpResponseBucket(Bucket inner, HttpBucketRequest request)
            : base(inner, request)
        {
            _nRedirects = request.MaxRedirects;
        }

        public new HttpBucketRequest Request => (HttpBucketRequest)base.Request;

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            if (_responseHeaders is null)
                await ReadHeaders().ConfigureAwait(false);

            if (_reader == null)
            {
                var (reader, doneAtEof) = GetBodyReader(Headers);

                _doneAtEof = doneAtEof;
                _reader = reader;
            }

            var bb = await _reader.ReadAsync(requested).ConfigureAwait(false);

            if (bb.IsEof)
            {
                while (_readUntilEof?.Count > 0)
                {
                    _reader = _readUntilEof.Pop();

                    while(0 != await _reader.ReadSkipAsync(int.MaxValue).ConfigureAwait(false))
                    {
                    }
                }

                if (Request.Channel != null)
                {
                    _reader.Dispose();
                    _reader = Bucket.Empty;

                    Request.ReleaseChannel();
                }
            }
            return bb;
        }

        private (Bucket reader, bool doneAtEof) GetBodyReader(WebHeaderDictionary headers)
        {
            var rdr = Inner;
            bool chunked = false;
            bool allowNext = false;

            // Transfer-Encoding, aka Hop by hop encoding. Typically 'chunked'
            if (headers[HttpResponseHeader.TransferEncoding] is string te)
            {
                foreach (var tEnc in te.Split(new[] { ',' }))
                {
                    if (string.Equals(tEnc, "chunked", StringComparison.OrdinalIgnoreCase))
                    {
                        allowNext = chunked = true;
                        rdr = new HttpDechunkBucket(rdr).NoClose();
                    }
                }
            }

            // RFC 7231 specifies that we should determine the message length via Transfer-Encoding
            // chunked, when both chunked and Content-Length are passed
            if (!chunked && headers[HttpResponseHeader.ContentLength] is string cl
                && long.TryParse(cl, out var contentLength) && contentLength >= 0)
            {
                rdr = rdr.Take(contentLength, true).NoClose();
                allowNext = true;
            }

            // Content-Encoding, aka end-to-end encoding. Typically 'gzip'
            if (headers[HttpResponseHeader.ContentEncoding] is string ce)
            {
                _readUntilEof ??= new Stack<Bucket>();
                foreach (var cEnc in ce.Split(new[] { ',' }))
                {
                    if (string.Equals(cEnc, "gzip", StringComparison.OrdinalIgnoreCase))
                    {
                        _readUntilEof.Push(rdr);
                        rdr = rdr.Decompress(BucketCompressionAlgorithm.GZip);
                    }
                    else if (string.Equals(cEnc, "deflate", StringComparison.OrdinalIgnoreCase))
                    {
                        _readUntilEof.Push(rdr);
                        rdr = rdr.Decompress(BucketCompressionAlgorithm.Deflate);
                    }
#if !NETFRAMEWORK
                    else if (string.Equals(cEnc, "br", StringComparison.OrdinalIgnoreCase))
                    {
                        _readUntilEof.Push(rdr);
                        rdr = rdr.Decompress(BucketCompressionAlgorithm.Brotli);
                    }
#endif
                    /*
                    else if (string.Equals(cEnc, "zstd", StringComparison.OrdinalIgnoreCase))
                    {
                        rdr = rdr.Decompress(BucketCompressionAlgorithm.Zstd); // Easy to implement via https://www.nuget.org/packages/ZstdSharp.Port
                    }
                    */
                }
            }

            return (rdr, !allowNext);
        }

        public override BucketBytes Peek()
        {
            if (_reader is not null)
                return _reader.Peek();

            return base.Peek();
        }

        public override async ValueTask ReadHeaders()
        {
            if (_responseHeaders is not null)
                return;

            if (!HttpStatus.HasValue)
                await ReadStatusAsync().ConfigureAwait(false);

            _responseHeaders ??= await ReadHeaderSet().ConfigureAwait(false);
        }

        private async ValueTask<WebHeaderDictionary> ReadHeaderSet()
        {
            WebHeaderDictionary whc = new WebHeaderDictionary();
            var (bb, eol) = await Inner.ReadUntilEolFullAsync(ResponseEol).ConfigureAwait(false);
            while (bb.Length - eol.CharCount() > 0)
            {
                string line = bb.ToUTF8String(eol);

                string[] parts = line.Split(new[] { ':' }, 2);

                whc[parts[0]] = parts[1].Trim();

                (bb, eol) = await Inner.ReadUntilEolFullAsync(ResponseEol).ConfigureAwait(false);
            }

            return whc;
        }

        public async ValueTask<int> ReadStatusAsync()
        {
            if (HttpStatus.HasValue)
                return HttpStatus.Value!;

            while (true)
            {
                var (bb, eol) = await Inner.ReadUntilEolFullAsync(ResponseEol).ConfigureAwait(false);

                string line = bb.ToASCIIString(eol);

                var parts = line.Split(new[] { ' ' }, 3);

                if (parts[0].StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase) && parts.Length == 3)
                    HttpVersion = parts[0].Substring(1);
                else
                    throw new HttpBucketException($"No HTTP result: {line}");

                if (int.TryParse(parts[1], out var status) && status >= 100 && status < 1000)
                {
                    switch (status)
                    {
                        case (int)HttpStatusCode.MovedPermanently:
                        case (int)HttpStatusCode.Found:
                        case (int)HttpStatusCode.TemporaryRedirect:
#if !NETFRAMEWORK
                        case (int)HttpStatusCode.PermanentRedirect:
#else
                        case 308:
#endif
                            if (Request.FollowRedirects && _nRedirects > 0)
                            {
                                _nRedirects--;
                                if (await HandleRedirect().ConfigureAwait(false))
                                    continue;
                            }
                            break;
                        case (int)HttpStatusCode.Unauthorized:
                            _authFailed?.Invoke();
                            _authFailed = null;
                            if (await HandleAuthorization().ConfigureAwait(false))
                                continue;
                            break;
                    }

                    HttpStatus = status;

                    if (_succes != null && status >= 200 && status < 400)
                    {
                        _succes.Invoke();
                        _succes = null;
                    }
                }
                else
                    throw new HttpBucketException($"No Proper HTTP status: {line}");

                HttpMessage = parts[2];
                return status;
            }
        }

        public override long? Position => _reader?.Position;

        public override ValueTask<long?> ReadRemainingBytesAsync()
        {
            if (_reader == null)
                return default;

            return _reader.ReadRemainingBytesAsync();
        }

        IEnumerable<(string username, string password, string q, Action success, Action failed)> WalkAuthorization(Uri uri, string realm)
        {
            var handlers = Request.GetBasicAuthenticationHandlers();
            List<BasicHandler> hlrs = new List<BasicHandler>();
            var items = new System.Collections.Hashtable();

            if (handlers is MulticastDelegate md)
            {
                hlrs.AddRange(md.GetInvocationList().Cast<BasicHandler>());

                for (int i = 0; i < hlrs.Count; i++)
                {
                    if (hlrs[i] is MulticastDelegate md2)
                    {
                        var p = md.GetInvocationList();

                        if (p.Length != 1 || p[0] != md2)
                        {
                            hlrs.RemoveAt(i);

                            hlrs.InsertRange(i, p.Cast<BasicHandler>());
                            i--;
                            continue;
                        }
                    }
                }
            }
            else if (handlers is not null)
                hlrs.Add(handlers);

            foreach (var h in hlrs)
            {
                BasicBucketAuthenticationEventArgs? e;
                do
                {
                    e = new BasicBucketAuthenticationEventArgs(uri, realm, items);
                    h.Invoke(this, e);

                    if (e.Handled)
                    {
                        yield return (e.Username ?? "", e.Password ?? "", "Basic", e.OnSucceeded, e.OnFailed);
                    }
                }
                while (e.Continue);
            }
        }

        IEnumerator<(string username, string password, string q, Action success, Action failed)>? _authState;

        private async ValueTask<bool> HandleAuthorization()
        {
            // Status line is read.. Read headers to allow authenticating in a new request
            var headers = await ReadHeaderSet().ConfigureAwait(false);

            string realm;

            if (headers[HttpResponseHeader.WwwAuthenticate] is string wwwAuthenticate)
            {
                var tk = wwwAuthenticate.Split(new[] { ' ' }, 2);
                string type = tk[0];

                switch (type.ToUpperInvariant())
                {
                    case "BASIC":
                        {
                            int n = tk[1].IndexOf("realm=\"", StringComparison.OrdinalIgnoreCase);

                            if (n >= 0)
                                realm = tk[1].Substring(n + 6, tk[1].IndexOf('\"', 7) - 6);
                            else
                                realm = "";
                        }
                        break;
                    default:
                        _responseHeaders = headers;
                        return false; // Just handle request
                }
            }
            else
            {
                _responseHeaders = headers;
                return false; //
            }

            _authState ??= WalkAuthorization(Request.RequestUri, realm).GetEnumerator();

            if (!_authState.MoveNext())
            {
                _responseHeaders = headers;
                return false; // Just handle request
            }

            var (reader, noClose) = GetBodyReader(headers);

            using (reader)
                await reader.ReadSkipUntilEofAsync().ConfigureAwait(false);

            var c = _authState.Current;

            Request.Headers[HttpRequestHeader.Authorization] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{c.username}:{c.password}"));

            Request.Channel!.Writer.Write(Request.CreateRequest()); // Request same page again
            return true; // Read next result
        }

        internal void HandlePreAuthenticate(HttpBucketRequest bucketHttpRequest)
        {
            if (_authState != null)
                throw new InvalidOperationException();

            _authState = WalkAuthorization(Request.RequestUri, "\"pre-authenticate\"").GetEnumerator();

            if (!_authState.MoveNext())
                return; // Nothing to do

            var c = _authState.Current;

            Request.Headers[HttpRequestHeader.Authorization] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{c.username}:{c.password}"));
            _succes += c.success;
            _authFailed += c.failed;
        }

        private async ValueTask<bool> HandleRedirect(bool keepMethod = true)
        {
            // We got an HTTP/1.1 301 Moved or similar

            var headers = await ReadHeaderSet().ConfigureAwait(false);

            Uri? newUri;
            if (headers[HttpResponseHeader.Location] is string location
                && Uri.TryCreate(location, UriKind.RelativeOrAbsolute, out var locationUri)
                && Uri.TryCreate(Request.RequestUri, locationUri, out newUri))
            {
                //
            }
            else if (headers[HttpResponseHeader.ContentLocation] is string location2
                && Uri.TryCreate(location2, UriKind.RelativeOrAbsolute, out var location2Uri)
                && Uri.TryCreate(Request.RequestUri, location2Uri, out newUri))
            {
                //
            }
            else
            {
                _responseHeaders = headers;
                return false;
            }

            var (reader, _) = GetBodyReader(headers);
            await reader.ReadSkipUntilEofAsync().ConfigureAwait(false);

            if (Request.RequestUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped) == newUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped))
            {
                // Re-use existing connection
                Request.UpdateUri(newUri);

                Request.Channel!.Writer.Write(Request.CreateRequest()); // Request same page again
                return true;
            }
            else
            {
                await Request.RunRedirect(newUri, keepMethod, this).ConfigureAwait(false);
                return false;
            }
        }

        public override bool SupportsHeaders => true;
        public override WebHeaderDictionary Headers => _responseHeaders!;

        public override long ContentLength
        {
            get
            {
                var v = Headers;
                if (v is not null && v[HttpResponseHeader.ContentLength] is string cl
                    && long.TryParse(cl, out var contentLength) && contentLength >= 0)
                {
                    return contentLength;
                }

                return -1;
            }
        }

        public override string? ContentType
        {
            get
            {
                var v = Headers;
                if (v is not null && v[HttpResponseHeader.ContentType] is string contentType)
                {
                    return contentType;
                }

                return null;
            }
        }
    }
}
