****Generic bucket library****

This library provides a generalized zero-copy lazy bucket implementation to allow reading information
chunks at a time. The buckets are expliclity designed to be minimalistic and stackable.

Every bucket must at least implement a ReadAsync(int requested=MaxRead) implementation that allows reading some bytes from the bucket. The caller provides a maximum number of bytes that
it can handle as response, while the implementation provides as many bytes as possible without doing additional work such as copying. The implementation
returns at least one byte or the special EOF marker when it can provide data, otherwise it waits -using the .Net async patter- until it can provide at least
one byte. The data returned by the Bucket is valid until the next .Read*Async() call.

To help buckets that read from other buckets there are a number of optional extensions. 

- The method Peek() returns the data the next read will provide, without actually reading the data.
- The method ReadSkipAsync() allows skipping a number of bytes. By default by just reading the data, but implementations may do this more efficient.
- The method ReadRemainingAsync() returns the remaining number of bytes in the bucket (if known).
- The property Position returns an absolute position within the data
- The method Reset() -when implemented+supported- allows resetting the bucket to allow restarting the read operation
- The method Duplicate() -when implemented+supported- allows duplicating the bucket to allow reading the data twice
- The method ReadUntilEolAsync() allows reading data from the bucket until the next newline.

Technically all of these methods are optional, but most have default implementations and/or properties to check if the features are supported. Via many helper and conveniance
methods quite a few features are added on top. Things like combining results, buffering, filtering and EOL normalization can easily be stacked.

Many basic bucket types are provided.
* file bucket
* memory bucket
* socket bucket
* aggregate bucket
* compression and decompression (ZLib, Deflate, GZip, BZip)
* hash bucket
* several kind of limit buckets
* trace and verify buckets (for debugging bucket issues
* buffer buckets
* text normalization

This library provides the bare layer for all these features, and is further extended via the AmpScmp.Buckets.Git, AmpScmp.Buckets.Security, AmpScm.Buckets.Subversion
packages to provide several file format implementations.

The AmpScm.Git.Repository package brings all of these packages together in a fully managed git repository implementation that can directly read all the git
fileformats using the transformations provided by this library.
