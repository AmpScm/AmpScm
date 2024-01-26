using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Client.Buckets;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Client;

public class HttpBucketWebRequest : BucketWebRequest
{
    internal BucketClientChannel? Channel { get; set; }
    internal int MaxRedirects { get; set; } = 10;

    private HttpResponseBucket? _redirectResponse;

    internal HttpBucketWebRequest(BucketWebClient client, Uri uri) : base(client, uri)
    {
    }

    private protected HttpBucketWebRequest(BucketWebClient client, Uri uri, bool forHttps) : base(client, uri)
    {

    }

    public override async ValueTask<ResponseBucket> GetResponseAsync()
    {
        var channel = await SetupChannel().ConfigureAwait(false);

        var response = new HttpResponseBucket(channel.Reader, this, Channel?.ReadOneEol ?? false);

        if (PreAuthenticate)
            response.HandlePreAuthenticate(this);

        channel.Writer.Write(CreateRequest());

        while (true)
        {
            await response.ReadStatusAsync().ConfigureAwait(false);

            if (_redirectResponse != null)
            {
                response = _redirectResponse;
                _redirectResponse = null;
                continue;
            }

            return response;
        }
    }

    private Encoding RequestEncoding { get; set; } = Encoding.UTF8;

    private protected async ValueTask<BucketClientChannel> SetupChannel()
    {
        if (Client.TryGetChannel(RequestUri, out var channel))
        {
            return Channel = channel!;
        }
        else
        {
            var (reader, writer) = await CreateChannel().ConfigureAwait(false);

            return Channel = CreateChannel(reader, writer);
        }
    }

    private protected virtual async ValueTask<(Bucket Reader, IBucketWriter Writer)> CreateChannel()
    {
        Socket s = new Socket(SocketType.Stream, ProtocolType.Tcp);

        try
        {
            var sb = new SocketBucket(s);

            await sb.ConnectAsync(RequestUri.Host, RequestUri.Port).ConfigureAwait(false);

            return (sb, sb);
        }
        catch
        {
            s.Dispose();
            throw;
        }
    }

    internal void ReleaseChannel(bool readOneEol)
    {
        try
        {
            Channel?.Release(readOneEol);
        }
        finally
        {
            Channel = null;
        }
    }

    private protected virtual BucketClientChannel CreateChannel(Bucket reader, IBucketWriter writer)
    {
        return new BucketClientChannel(Client, RequestUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped), reader, writer);
    }

    internal virtual Bucket CreateRequest()
    {
        Encoding enc = RequestEncoding;

#pragma warning disable CA2000 // Dispose objects before losing scope
        return enc.GetBytes((Method ?? "GET") + " ").AsBucket()
            + enc.GetBytes(RequestUri.GetComponents(UriComponents.PathAndQuery, UriFormat.UriEscaped)).AsBucket()
            + enc.GetBytes(" HTTP/1.1\r\n").AsBucket()
            + CreateHeaders(RequestUri.Host);
#pragma warning restore CA2000 // Dispose objects before losing scope
    }

    protected virtual Bucket CreateHeaders(string hostName)
    {
        var bucket = Bucket.Empty;
        Encoding enc = Encoding.UTF8;

        if (!Headers.Contains(HttpRequestHeader.Host))
        {
            bucket += enc.GetBytes("Host: ").AsBucket()
                    + enc.GetBytes(RequestUri.Host).AsBucket()
                    + enc.GetBytes("\r\n").AsBucket();
        }

        if (!Headers.Contains(HttpRequestHeader.AcceptEncoding))
        {
#if !NETFRAMEWORK
            bucket += Bucket.Create.FromUTF8("Accept-Encoding: gzip, deflate, br\r\n");
#else
            bucket += Bucket.Create.FromUTF8("Accept-Encoding: gzip, deflate\r\n");
#endif
        }

        bucket += Headers.ToByteArray().AsBucket(); // Includes the final \r\n to end the request headers

        return bucket;
    }

    internal async ValueTask RunRedirect(Uri newUri, bool keepMethod, bool readOneEol)
    {
        UpdateUri(newUri);

        var c = Client.CreateRequest(newUri);

        (c as HttpBucketWebRequest)?.CopyFrom(this);

        ReleaseChannel(readOneEol);
        _redirectResponse = (HttpResponseBucket)await c.GetResponseAsync().ConfigureAwait(false);
    }

    private void CopyFrom(HttpBucketWebRequest from)
    {
        foreach (string k in from.Headers)
        {
            Headers[k] = from.Headers[k];
        }
        PreAuthenticate = from.PreAuthenticate;
        MaxRedirects = from.MaxRedirects - 1;
    }
}
