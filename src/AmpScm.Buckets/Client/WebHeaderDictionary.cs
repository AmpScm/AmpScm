using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using BaseWhc = System.Net.WebHeaderCollection;

namespace AmpScm.Buckets.Client;

[Serializable]
public sealed class WebHeaderDictionary : WebHeaderCollection, IEnumerable<string>, IDictionary<string, string>
{
    [field: NonSerialized]
    public new KeysCollection Keys { get; }

    bool ICollection<KeyValuePair<string, string>>.IsReadOnly => false;

    ICollection<string> IDictionary<string, string>.Keys => Keys;
    ICollection<string> IDictionary<string, string>.Values => new ValuesCollection(this);

    public WebHeaderDictionary()
    {
        Keys = new KeysCollection(this);
    }

    [Obsolete("Just for legacy .Net compatibilty")]
    private WebHeaderDictionary(SerializationInfo serializationInfo, StreamingContext streamingContext)
        : base(serializationInfo, streamingContext)
    {
        Keys = new KeysCollection(this);
    }

    private BaseWhc BaseWhc => this;

    string IDictionary<string, string>.this[string key]
    {
        get
        {
            if (key is null)
                throw new ArgumentNullException(nameof(key));
            string? v = BaseWhc[key];

            if (v is null)
                throw new KeyNotFoundException();

            return v;
        }
        set
        {
            this[key] = value;
        }
    }


    public new IEnumerator<string> GetEnumerator()
    {
        foreach(string? s in BaseWhc.Keys)
        {
            yield return s!;
        }
    }

    public bool Contains(HttpRequestHeader requestHeader)
    {
        return base[requestHeader] != null;
    }

    public bool Contains(HttpResponseHeader responseHeader)
    {
        return base[responseHeader] != null;
    }

    public bool Contains(string header)
    {
        return base[header] != null;
    }

    bool IDictionary<string, string>.ContainsKey(string key)
    {
        return Contains(key);
    }

    bool IDictionary<string, string>.Remove(string key)
    {
        bool removed = Contains(key);
        base.Remove(key);
        return removed;
    }

    bool IDictionary<string, string>.TryGetValue(string key, out string value)
    {
        value = base[key]!;
        return (value != null);
    }

    void ICollection<KeyValuePair<string, string>>.Add(KeyValuePair<string, string> item)
    {
        base[item.Key] = item.Value;
    }

    bool ICollection<KeyValuePair<string, string>>.Contains(KeyValuePair<string, string> item)
    {
        return base[item.Key] == item.Value;
    }

    void ICollection<KeyValuePair<string, string>>.CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
    {
        foreach(var kv in (IEnumerable<KeyValuePair<string,string>>)this)
        {
            array[arrayIndex++] = kv;
        }
    }

    bool ICollection<KeyValuePair<string, string>>.Remove(KeyValuePair<string, string> item)
    {
        string? v = base[item.Key];
        if (v == item.Value)
        {
            base.Remove(item.Key);
            return true;
        }
        else
            return false;
    }

    IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
    {
        foreach(string k in this)
        {
            yield return new KeyValuePair<string, string>(k, base[k]!);
        }
    }

#pragma warning disable CA1034 // Nested types should not be visible
    public new sealed class KeysCollection : IReadOnlyList<string>, ICollection<string>
#pragma warning restore CA1034 // Nested types should not be visible
    {
        private readonly WebHeaderDictionary _whc;
        internal KeysCollection(WebHeaderDictionary whc)
        {
            _whc = whc ?? throw new ArgumentNullException(nameof(whc));
        }

        private BaseWhc BaseWhc => _whc;

        public string this[int index] => BaseWhc.Keys[index]!;

        public int Count => _whc.Count;

        public bool IsReadOnly => true;

        public void Add(string item)
        {
            throw new InvalidOperationException();
        }

        public void Clear()
        {
            throw new InvalidOperationException();
        }

        public bool Contains(string item)
        {
            return _whc.Contains(item);
        }

        public void CopyTo(string[] array, int arrayIndex)
        {
            ((ICollection)BaseWhc.Keys).CopyTo(array, arrayIndex);
        }

        public IEnumerator<string> GetEnumerator()
        {
            return _whc.GetEnumerator();
        }

        public bool Remove(string item)
        {
            bool v = _whc.Contains(item);
            _whc.Remove(item);
            return v;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _whc.GetEnumerator();
        }
    }

    private sealed class ValuesCollection : IReadOnlyList<string>, ICollection<string>
    {
        private readonly WebHeaderDictionary _whc;
        internal ValuesCollection(WebHeaderDictionary whc)
        {
            _whc = whc ?? throw new ArgumentNullException(nameof(whc));
        }

        private BaseWhc BaseWhc => _whc;

        public string this[int index] => _whc[index]!;

        public int Count => _whc.Count;

        public bool IsReadOnly => true;

        public void Add(string item)
        {
            throw new InvalidOperationException();
        }

        public void Clear()
        {
            throw new InvalidOperationException();
        }

        public bool Contains(string item)
        {
            return _whc.Contains(item);
        }

        public void CopyTo(string[] array, int arrayIndex)
        {
            foreach(string? v in BaseWhc)
            {
                array[arrayIndex++] = BaseWhc[v!]!;
            }
        }

        public IEnumerator<string> GetEnumerator()
        {
            foreach (string? v in BaseWhc)
            {
                yield return BaseWhc[v]!;
            }
        }

        public bool Remove(string item)
        {
            throw new InvalidOperationException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
