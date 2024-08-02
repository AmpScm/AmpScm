****Git bucket library****

Implements reading (and some writing) of git specific formats as a layer over the AmpScm.Buckets library.

This allows reading git data completely streaming, so no large memory buffers are needed for processing,
unlike most other git libraries.

The AmpScm.Git.Repository and AmpScm.Git.Client libraries are built on top of this library.