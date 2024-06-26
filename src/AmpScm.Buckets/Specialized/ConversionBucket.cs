﻿using System.Diagnostics;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized;

public abstract class ConversionBucket : WrappingBucket, IBucketPoll
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private BucketBytes _remaining;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _skipFirst;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private BucketBytes _readLeft;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private long _position;

    protected ConversionBucket(Bucket source) : base(source)
    {
    }

    //public ValueTask<BucketBytes> PollAsync(int minRequested = 1)
    //{
    //    return Peek();
    //}

    public override BucketBytes Peek()
    {
        if (!_remaining.IsEmpty)
            return _remaining;

        while (!_readLeft.IsEmpty)
        {
            int origLeft = _readLeft.Length;
            _remaining = ConvertData(ref _readLeft, final: false);

            if (!_remaining.IsEmpty || _readLeft.Length == origLeft)
                return _remaining;
        }

        var bb = SourcePeek();

        if (!bb.IsEmpty && _skipFirst > 0)
            bb = bb.Slice(_skipFirst);

        if (bb.IsEmpty)
            return bb;

        int len = bb.Length;
        _remaining = ConvertData(ref bb, final: false);

        _skipFirst += (len - bb.Length);

        return _remaining;
    }

    protected abstract BucketBytes ConvertData(ref BucketBytes sourceData, bool final);

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        if (!_remaining.IsEmpty)
        {
            var r = _remaining.Slice(0, Math.Min(requested, _remaining.Length));
            _remaining = _remaining.Slice(r.Length);
            _position += r.Length;
            return r;
        }
        while (_skipFirst > 0)
        {
            int skipped = await Source.ReadSkipAsync(_skipFirst).ConfigureAwait(false);
            _skipFirst -= skipped;
        }

        while (!_readLeft.IsEof)
        {
            if (_readLeft.IsEmpty && !_readLeft.IsEof)
                _readLeft = await SourceReadAsync(ConvertRequested(requested)).ConfigureAwait(false);

            bool final = _readLeft.IsEof;

#if DEBUG
            Debug.Assert(!_readLeft.IsEmpty || _readLeft.IsEof, $"{Source} bucket reports empty instead of eof");
#endif

            _remaining = ConvertData(ref _readLeft, final);

            if (!_remaining.IsEmpty)
            {
                var r = _remaining.Slice(0, Math.Min(requested, _remaining.Length));
                _remaining = _remaining.Slice(r.Length);
                _position += r.Length;
                return r;
            }
            else if (final)
                break;
        }

        return BucketBytes.Eof;
    }

    public async ValueTask<BucketBytes> PollAsync(int minRequested = 1)
    {
        if (!_remaining.IsEmpty)
            return _remaining;

        while (_skipFirst > 0)
        {
            int skipped = await Source.ReadSkipAsync(_skipFirst).ConfigureAwait(false);
            _skipFirst -= skipped;
        }

        do
        {
            if (_readLeft.IsEmpty)
                _readLeft = await SourceReadAsync(ConvertRequested(Math.Max(512, minRequested))).ConfigureAwait(false);

            _remaining = ConvertData(ref _readLeft, _readLeft.IsEof);

            if (!_remaining.IsEmpty)
                return _remaining;
        }
        while (!_readLeft.IsEof);

        return BucketBytes.Eof;
    }

    protected virtual ValueTask<BucketBytes> SourceReadAsync(int requested = MaxRead)
        => Source.ReadAsync(requested);

    protected virtual BucketBytes SourcePeek()
        => Source.Peek();

    protected virtual int ConvertRequested(int requested)
    {
        return requested;
    }

    public override void Reset()
    {
        if (!CanReset)
            throw new InvalidOperationException();

        Source.Reset();
        _position = 0;
    }

    public override long? Position => _position;
}
