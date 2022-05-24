﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace AmpScm.Buckets
{
    public partial class FileBucket
    {
        sealed class FileHolder : IDisposable
        {
#if !NET6_0_OR_GREATER
            readonly Stack<FileStream> _keep;
#endif
            readonly FileStream _primary;
            readonly SafeFileHandle _handle;
            readonly bool _asyncWin;
            readonly Stack<FileWaitHandler> _waitHandlers;
            long? _length;
            Action _disposers;
            int _nRefs;

            public FileHolder(FileStream primary, string path)
            {
                _primary = primary ?? throw new ArgumentNullException(nameof(primary));
                Path = path ?? throw new ArgumentNullException(nameof(path));

#if !NET6_0_OR_GREATER
                _keep = new();

                if (primary.IsAsync)
                    _keep.Push(primary);
#endif

                _disposers = _primary.Dispose;
                _waitHandlers = default!;
                _handle = primary.SafeFileHandle;

#if NET6_0_OR_GREATER
                if (primary.IsAsync)
                    _length = RandomAccess.GetLength(_handle);
#endif
            }

            public string Path { get; }


#if NET5_0_OR_GREATER
            [SupportedOSPlatform("windows")]
#endif
            public FileHolder(SafeFileHandle handle, string path)
            {
                if (handle?.IsInvalid ?? true)
                    throw new ArgumentNullException(nameof(handle));
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                    throw new InvalidOperationException("Only supported on Windows at this time");

                _primary = default!;
                _handle = handle;
                Path = path ?? throw new ArgumentNullException(nameof(path));
                _asyncWin = true;
                _waitHandlers = new Stack<FileWaitHandler>();

                _disposers = _handle.Dispose;
                _handle = handle;

#if NET6_0_OR_GREATER
                _length = RandomAccess.GetLength(_handle);
#else
                _length = NativeMethods.GetFileSize(_handle);
                _keep = default!;
#endif
            }

            ~FileHolder()
            {
                var d = _disposers;
                _disposers = null!;
                foreach (Action a in d.GetInvocationList())
                {
#pragma warning disable CA1031 // Do not catch general exception types
                    try
                    {
                        a.Invoke(); // But do release memory we allocated explicitly
                    }
                    catch
                    { }
#pragma warning restore CA1031 // Do not catch general exception types
                }
            }

            public void AddRef()
            {
                Interlocked.Increment(ref _nRefs);
            }

            public void Release()
            {
                if (Interlocked.Decrement(ref _nRefs) == 0)
                {
                    Dispose();
                }
            }

            // Only called from Release, but called Dispose to aid code analyzers
            public void Dispose()
            {
                try
                {
#if !NET6_0_OR_GREATER
                    while (_keep?.Count > 0)
                    {
                        _keep.Pop().Dispose();
                    }
#endif
                    var d = _disposers;
                    _disposers = () => { };
                    d.Invoke();
                }
                finally
                {
                    GC.SuppressFinalize(this);
                }
            }

            public ValueTask<int> ReadAtAsync(long fileOffset, byte[] buffer, int requested)
            {
                if (requested <= 0)
                    throw new ArgumentOutOfRangeException(nameof(requested), requested, "Requested bytes must be at least 1");
                else if (fileOffset > 0 && fileOffset >= Length)
                    return new ValueTask<int>(0);

                if (_asyncWin)
#pragma warning disable CA1416 // Validate platform compatibility
                    return AsyncWinReadAsync(fileOffset, buffer, requested);
#pragma warning restore CA1416 // Validate platform compatibility
#if NET6_0_OR_GREATER
                else
                    return RandomAccess.ReadAsync(_handle, buffer.AsMemory(0, requested), fileOffset);
#else
                else if (_primary.IsAsync)
                    return TrueReadAtAsync(fileOffset, buffer, requested);
                else
                {
                    using (GetFileStream(out var p))
                    {
                        if (p.Position != fileOffset)
                            p.Position = fileOffset;

#pragma warning disable CA1849 // Call async methods when in an async method
                        int r = p.Read(buffer, 0, requested);
#pragma warning restore CA1849 // Call async methods when in an async method

                        return new ValueTask<int>(r);
                    }
                }
#endif
            }

#if NET5_0_OR_GREATER
            [SupportedOSPlatform("windows")]
#endif
            public ValueTask<int> AsyncWinReadAsync(long offset, byte[] buffer, int readLen)
            {
                FileWaitHandler waitHandler;

                if (readLen < 1)
                    throw new ArgumentOutOfRangeException(nameof(readLen));

                long fp = offset + readLen;
                if (fp > _length!.Value)
                {
                    long rl = (_length.Value - offset);

                    if (rl < 1)
                        return new ValueTask<int>(0);
                    readLen = (int)rl;
                }

                lock (_waitHandlers)
                {
                    if (_waitHandlers.Count > 0)
                        waitHandler = _waitHandlers.Pop();
                    else
                    {
                        int sz = Marshal.SizeOf<NativeOverlapped>();
                        IntPtr p = Marshal.AllocCoTaskMem(Marshal.SizeOf<NativeOverlapped>() * 16);

                        if (p == IntPtr.Zero)
                            throw new InvalidOperationException();

                        for (int i = 1; i < 16; i++)
                        {
                            var f = new FileWaitHandler((IntPtr)((long)p + i * sz));
                            _disposers += f.Dispose;
                            _waitHandlers.Push(f);
                        }
                        waitHandler = new FileWaitHandler(p); // And keep the last one
                        _disposers += waitHandler.Dispose;

                        _disposers += () => Marshal.FreeCoTaskMem(p);
                    }
                }
                TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();

                using (waitHandler.Alloc(this, tcs, offset, buffer, out var lpOverlapped))
                {
                    if (NativeMethods.ReadFile(_handle, buffer, readLen, out var read, lpOverlapped))
                    {
                        // Unlikely direct succes case. No result queued
                        waitHandler.ReleaseOne();
                        return new ValueTask<int>((int)read); // Done reading
                    }
                    else
                    {
                        int err = Marshal.GetLastWin32Error();

                        if (err == 997 /* Pending IO */)
                        {
                            if (NativeMethods.GetOverlappedResult(_handle, lpOverlapped, out uint bytes, false))
                            {
                                // Typical all-data cached in filecache case on Windows 10/11 2022-04
                                return new ValueTask<int>((int)bytes); // Return succes. Task will release lpOverlapped
                            }
                            else
                                return new ValueTask<int>(tcs.Task); // Wait for task
                        }
                        else
                        {
                            waitHandler.ReleaseOne();

                            throw new BucketException($"ReadFile failed on {Path}, error={err}", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
                        }
                    }
                }
            }

            public long Length => _length ??= _primary.Length;

#if !NET6_0_OR_GREATER
            public async ValueTask<int> TrueReadAtAsync(long offset, byte[] buffer, int readLen)
            {
                bool primary = false;
                try
                {
                    using (GetFileStream(out var p))
                    {
                        primary = (p == _primary);
                        if (p.Position != offset)
                            p.Position = offset;

#if !NETFRAMEWORK
                        var r = await p.ReadAsync(buffer.AsMemory(0, readLen)).ConfigureAwait(false);
#else
                        var r = await p.ReadAsync(buffer, 0, readLen).ConfigureAwait(false);
#endif
                        return r;
                    }
                }
                catch (Exception e) when (primary)
                {
                    throw new BucketException("Error reading primary", e);
                }
            }

            Returner GetFileStream(out FileStream f)
            {
                lock (_keep)
                {
                    FileStream p = _keep.Count > 0 ? _keep.Pop() : NewStream();
                    var r = new Returner(this, p); // Do this before setting the out argument
                    f = p;

                    return r;
                }
            }

            private FileStream NewStream()
            {
                return new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, true);
            }

            private void Return(FileStream f)
            {
                if (f == null)
                    throw new ArgumentNullException(nameof(f));

                lock (_keep)
                {
                    if (_keep.Count > 4 && (f != _primary))
                    {
                        if (_primary.IsAsync) // All file instances share the same handle
                        {
#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
                            GC.SuppressFinalize(f);
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
                        }
                        else
                        {

                            f.Dispose();
                        }
                    }
                    else
                        _keep.Push(f);
                }
            }

            sealed class Returner : IDisposable
            {
                readonly FileStream _fs;
                FileHolder? _fh;

                public Returner(FileHolder fh, FileStream fs)
                {
                    _fh = fh;
                    _fs = fs;
                }

                public void Dispose()
                {
                    _fh?.Return(_fs);
                    _fh = null;
                }
            }
#endif

#if NET5_0_OR_GREATER
            [SupportedOSPlatform("windows")]
#endif
            sealed class FileWaitHandler : IDisposable
            {
                readonly IntPtr _overlapped;
                FileHolder? _holder;
                TaskCompletionSource<int>? _tcs;
                GCHandle _pin;
                EventWaitHandle _eventWaitHandle;
                RegisteredWaitHandle? _registeredWaitHandle;
                int _c;

                public FileWaitHandler(IntPtr overlapped)
                {
                    _overlapped = overlapped;
                    _eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
                }

                void OnSignal(object? state, bool timedOut)
                {
                    Debug.Assert(_holder is not null);
                    try
                    {
                        if (NativeMethods.GetOverlappedResult(_holder!._handle, _overlapped, out var t, false))
                        {
                            _tcs!.TrySetResult((int)t);
                        }
                        else
                            _tcs!.SetException(new InvalidOperationException("GetOverlappedResult failed"));
                    }
                    finally
                    {
                        ReleaseOne();
                    }
                }

                public void ReleaseOne()
                {
                    var c = Interlocked.Decrement(ref _c);
                    if (c == 0)
                    {
                        FileHolder h = _holder!;
                        _holder = null;

                        Debug.Assert(h != null);

                        // When both the callback and caller are done, release overlapped struct and mutex for re-use
                        if (_pin.IsAllocated)
                            _pin.Free();

                        _tcs = null;

                        lock (h!._waitHandlers)
                            h._waitHandlers.Push(this);

                        h.Release();
                    }
                }

                public Releaser Alloc(FileHolder holder, TaskCompletionSource<int> tcs, long offset, object toPin, out IntPtr lpOverlapped)
                {
                    if (holder is null)
                        throw new ArgumentNullException(nameof(holder));
                    if (_pin.IsAllocated)
                        throw new InvalidOperationException();

                    _holder = holder;
                    _holder.AddRef();

                    if (toPin is not null)
                        _pin = GCHandle.Alloc(toPin, GCHandleType.Pinned);

                    _tcs = tcs;
                    _c = 2; // Release in callback and caller

                    _registeredWaitHandle ??= ThreadPool.RegisterWaitForSingleObject(_eventWaitHandle, OnSignal, this, -1, false);

                    NativeOverlapped nol = default;
                    nol.OffsetLow = (int)(offset & uint.MaxValue);
                    nol.OffsetHigh = (int)(offset >> 32);
                    nol.EventHandle = _eventWaitHandle.SafeWaitHandle.DangerousGetHandle();

                    Marshal.StructureToPtr(nol, _overlapped, false);

                    lpOverlapped = _overlapped;
                    return new Releaser(this);
                }

                public void Dispose()
                {
                    if (_pin.IsAllocated)
                        _pin.Free();

                    _registeredWaitHandle?.Unregister(_eventWaitHandle);
                    _registeredWaitHandle = null;

                    _eventWaitHandle?.Dispose();
                    _eventWaitHandle = null!;
                }

                public sealed class Releaser : IDisposable
                {
                    FileWaitHandler fileWaitHandler;

                    public Releaser(FileWaitHandler fileWaitHandler)
                    {
                        this.fileWaitHandler = fileWaitHandler;
                    }

                    public void Dispose()
                    {
                        fileWaitHandler.ReleaseOne();
                        fileWaitHandler = null!;
                    }
                }
            }

            static class NativeMethods
            {
                [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
                [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
                public static extern SafeFileHandle CreateFileW(
                    [MarshalAs(UnmanagedType.LPWStr)] string filename,
                    uint access,
                    uint share,
                    IntPtr securityAttributes,
                    uint creationDisposition,
                    uint flagsAndAttributes,
                    IntPtr templateFile);

                [DllImport("kernel32.dll", SetLastError = true)]
                [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
                public static extern bool ReadFile(SafeFileHandle hFile, [Out] byte[] lpBuffer,
                    int nNumberOfBytesToRead, out uint pRead, IntPtr pOverlapped);


                [DllImport("kernel32.dll", SetLastError = true)]
                [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
                [return: MarshalAs(UnmanagedType.Bool)]
                public static extern bool GetOverlappedResult(SafeFileHandle hFile, IntPtr lpOverlapped, out uint lpNumberOfBytesTransferred, [MarshalAs(UnmanagedType.Bool)] bool bWait);

#if !NET6_0_OR_GREATER
                [DllImport("kernel32.dll", SetLastError = true)]
                [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
                static extern bool GetFileSizeEx(SafeFileHandle hFile, out ulong size);

                internal static long GetFileSize(SafeFileHandle handle)
                {
                    if (GetFileSizeEx(handle, out var size))
                        return (long)size;
                    else
                        return 0;
                }
#endif
            }

            internal static SafeFileHandle OpenAsyncWin32(string path)
            {
                if (string.IsNullOrEmpty(path))
                    throw new ArgumentNullException(nameof(path));

                SafeFileHandle handle = NativeMethods.CreateFileW(path,
                    access: 0x80000000 /* GENERIC_READ */, // We want to read
                    share: 0x00000004 /* FILE_SHARE_DELETE */ | 0x00000001 /* FILE_SHARE_READ */, // Others can read, delete, rename, but we keep our file open
                    securityAttributes: IntPtr.Zero,
                    creationDisposition: 3 /* OPEN_EXISTING */,
                    0x80 /* Normal attributes */ | 0x40000000 /* FILE_FLAG_OVERLAPPED */,
                    IntPtr.Zero);

                if (!handle.IsInvalid)
                    return handle;
                else
                    throw new FileNotFoundException($"Couldn't open {path}", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
            }
        }
    }
}
