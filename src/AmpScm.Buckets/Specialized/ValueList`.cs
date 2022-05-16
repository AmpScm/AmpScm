using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    /// <summary>
    /// Very minimalistic array wrapper, stored in struct to allow usage in AggregateBucket
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal struct ValueList<T> : IEnumerable<T?>, IReadOnlyList<T?>
        where T : class
    {
        T?[] _items;
        int _start;
        public int Count { get; private set; }

        public ValueList()
        {
            _items = Array.Empty<T>();
            _start = Count = 0;
        }

        public void Add(T? item)
        {
            if (Count == 0)
            {
                _start = 0;
                if (_items.Length == 0)
                    _items = new T[8];

                _items[_start + Count++] = item;
            }
            else if (_start + Count < _items.Length)
            {
                _items[_start + Count++] = item;
            }
            else
            {
                var newArr = new T?[Math.Max(Count + Count / 2, Count + 8)];
                if (Count > 0)
                    Array.Copy(_items, _start, newArr, 4, Count);
                _start = 4;
                newArr[_start + Count++] = item;
                _items = newArr;
            }
        }

        public void AddRange(T?[] items, int skip = 0)
        {
            if (items.Length <= skip)
                return;

            if (Count == 0)
                _start = 0;

            if (_items.Length - _start - Count > items.Length)
            {
                Array.Copy(items, 0, _items, _start + Count, items.Length);
                Count += items.Length;
            }
            else
            {
                int c = _items.Length + items.Length;
                var newArr = new T?[Math.Min((c * 3) / 2, c + 8)];

                Array.Copy(_items, _start, newArr, 0, Count);
                Array.Copy(items, 0 + skip, newArr, Count - skip, items.Length);

                _items = newArr;

                _start = 0;
                Count += items.Length - skip;
            }
        }

        public void Insert(int index, T item)
        {
            if (index >= Count)
                Add(item);
            else if (index == 0 && _start > 0)
            {
                _items[--_start] = item;
            }
            else if (Count + _start < _items.Length)
            {
                if (_start == 0 && index == 0)
                {
                    Array.Copy(_items, 0, _items, 1, Count);
                    _items[_start] = item;
                    Count++;
                }
                else
                {
                    // Not needed for aggregate bucket, so leave as todo
                    throw new NotImplementedException();
                }
            }
            else
            {
                var newArr = new T?[Math.Max(_items.Length + _items.Length / 2, _items.Length + 8)];

                if (index == 0)
                {
                    Array.Copy(_items, _start, newArr, 4, Count);
                    newArr[_start = 3] = item;
                }
                else
                {
                    // Not needed for aggregate bucket, so leave as todo
                    throw new NotImplementedException();
                }

                Count++;
            }
        }

        public IEnumerator<T?> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
            {
                yield return _items[i + _start];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public T? this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return _items[index + _start];
            }
            set
            {
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                _items[index + _start] = value;
            }
        }

        internal void RemoveAt(int index)
        {
            if (Count == 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (index == 0)
            {
                _items[_start] = null;
                _start++;
                Count--;
            }
            else if (index == Count - 1)
            {
                _items[_start + index] = null;
                Count--;
            }
            else
                throw new NotImplementedException();

            if (Count == 0)
                _start = 0;
        }

        public void Clear()
        {
            for (int i = 0; i < Count; i++)
                _items[i + _start] = null;
            _start = 0;
        }
    }
}
