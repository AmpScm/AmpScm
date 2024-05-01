namespace AmpScm.Buckets.Client;

public class BucketAuthenticationEventArgs : EventArgs
{
    internal BucketAuthenticationEventArgs(Uri uri, string realm, System.Collections.Hashtable items)
    {
        Uri = uri;
        Realm = realm;
        Items = items;
    }

    public string Realm { get; }
    public Uri Uri { get; }

    public bool Handled { get; set; } = true;
    public bool Continue { get; set; }

    public event EventHandler<BucketAuthenticationEventArgs>? Succeeded;
    public event EventHandler<BucketAuthenticationEventArgs>? Failed;

    internal void OnSucceeded() => Succeeded?.Invoke(this, this);

    internal void OnFailed() => Failed?.Invoke(this, this);

    public System.Collections.Hashtable Items { get; }
}

public class BasicBucketAuthenticationEventArgs : BucketAuthenticationEventArgs
{
    internal BasicBucketAuthenticationEventArgs(Uri uri, string realm, System.Collections.Hashtable items)
        : base(uri, realm, items)
    {
    }

    public string? Username { get; set; }
    public string? Password { get; set; }
}
