using System;
using System.Collections.Generic;
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
        sealed class FileHolder
        {
            readonly Stack<FileStream> _keep = new Stack<FileStream>();
            readonly FileStream _primary;
            SafeFileHandle _handle;
            int _nRefs;
            long? _length;
            bool _asyncWin;
            Queue<FileWaitHandler> _waitHandlers;
            Action _disposers;

            public FileHolder(FileStream primary, string path)
            {
                _primary = primary ?? throw new ArgumentNullException(nameof(primary));
                Path = path ?? throw new ArgumentNullException(nameof(path));

                if (primary.IsAsync)
                    _keep.Push(primary);

                _disposers = _primary.Dispose;
                _waitHandlers = default!;
                _handle = primary.SafeFileHandle;
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
                _waitHandlers = new Queue<FileWaitHandler>();

                _disposers = _handle.Dispose;
                _handle = handle;

                _length = NativeMethods.GetFileSize(_handle);
            }

            public void AddRef()
            {
                _nRefs++;
            }

            public void Release()
            {
                _nRefs--;

                if (_nRefs >= 0)
                {
                    while (_keep.Count > 0)
                    {
                        var r = _keep.Pop();
#if NET6_0_OR_GREATER
                        if (!r.IsAsync)
#endif
                        {
                            r.Dispose();
                        }
                    }
                    _disposers.Invoke();
                }
            }

            public ValueTask<int> ReadAtAsync(long fileOffset, byte[] buffer, int requested)
            {
                if (requested <= 0)
                    throw new ArgumentOutOfRangeException(nameof(requested));
                else if (fileOffset > 0 && fileOffset >= Length)
                    return new ValueTask<int>(0);

                if (_asyncWin)
#pragma warning disable CA1416 // Validate platform compatibility
                    return AsyncWinReadAsync(fileOffset, buffer, requested);
#pragma warning restore CA1416 // Validate platform compatibility
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
                        waitHandler = _waitHandlers.Dequeue();
                    else
                    {
                        int sz = Marshal.SizeOf<NativeOverlapped>();
                        IntPtr p = Marshal.AllocCoTaskMem(Marshal.SizeOf<NativeOverlapped>() * 16);

                        if (p == IntPtr.Zero)
                            throw new InvalidOperationException();

                        for (int i = 1; i < 16; i++)
                        {
                            var f = new FileWaitHandler(this, (IntPtr)((long)p + i * sz));
                            _disposers += f.Dispose;
                            _waitHandlers.Enqueue(f);
                        }
                        waitHandler = new FileWaitHandler(this, p); // And keep the last one
                        _disposers += waitHandler.Dispose;

                        _disposers += () => Marshal.FreeCoTaskMem(p);
                    }
                }
                TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();

                using (waitHandler.Alloc(tcs, offset, buffer, out var lpOverlapped))
                {
                    if (NativeMethods.ReadFile(_handle, buffer, readLen, out var read, lpOverlapped))
                    {
                        // Unlikely direct succes case. No result queued
                        waitHandler.ReleaseOne();
                        return new ValueTask<int>((int)read); // Done reading
                    }
                    else if (Marshal.GetLastWin32Error() == 997 /* Pending IO */)
                    {
                        // Typical all-data cached in filecache case on Windows 10/11 2022-04
                        if (NativeMethods.GetOverlappedResult(_handle, lpOverlapped, out uint bytes, false))
                        {
                            return new ValueTask<int>((int)bytes); // Return succes. Task will release lpOverlapped
                        }
                        else
                            return new ValueTask<int>(tcs.Task); // Wait for task
                    }
                    else
                    {
                        waitHandler.ReleaseOne();

                        throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()) ?? new BucketException("ReadFileEx failed");
                    }
                }
            }

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
#if NET6_0_OR_GREATER
                if (_primary.IsAsync)
                    return new FileStream(_primary.SafeFileHandle, FileAccess.Read, 4096, true);
#endif
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
#if NET6_0_OR_GREATER
                        if (_primary.IsAsync) // All file instances share the same handle
                        {
#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
                            GC.SuppressFinalize(f);
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
                        }
                        else
#endif
                        {

                            f.Dispose();
                        }
                    }
                    else
                        _keep.Push(f);
                }
            }

            public long Length => _length ??= _primary.Length;


            sealed class Returner : IDisposable
            {
                FileHolder? _fh;
                FileStream _fs;

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

#if NET5_0_OR_GREATER
            [SupportedOSPlatform("windows")]
#endif
            sealed class FileWaitHandler : IDisposable
            {
                readonly FileHolder _holder;
                TaskCompletionSource<int>? _tcs;
                IntPtr _overlapped;
                GCHandle _pin;
                EventWaitHandle _eventWaitHandle;
                RegisteredWaitHandle? _registeredWaitHandle;
                int _c;


                public FileWaitHandler(FileHolder holder, IntPtr overlapped)
                {
                    _holder = holder;
                    _overlapped = overlapped;
                    _eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
                }

                void OnSignal(object? state, bool timedOut)
                {
                    _eventWaitHandle.Reset();

                    if (NativeMethods.GetOverlappedResult(_holder._handle, _overlapped, out var t, false))
                    {
                        _tcs!.TrySetResult((int)t);
                    }
                    else
                        _tcs!.SetException(new InvalidOperationException("GetOverlappedResult failed"));

                    ReleaseOne();
                }

                public void ReleaseOne()
                {
                    if (Interlocked.Decrement(ref _c) == 0)
                    {
                        // When both the callback and caller are done, release overlapped struct and mutex for re-use
                        if (_pin.IsAllocated)
                            _pin.Free();

                        _tcs = null;

                        lock (_holder._waitHandlers)
                            _holder._waitHandlers.Enqueue(this);
                    }
                }

                public Releaser Alloc(TaskCompletionSource<int> tcs, long offset, object toPin, out IntPtr lpOverlapped)
                {
                    if (_pin.IsAllocated)
                        throw new InvalidOperationException();

                    if (toPin is not null)
                        _pin = GCHandle.Alloc(toPin, GCHandleType.Pinned);
                    _tcs = tcs;

                    _registeredWaitHandle ??= ThreadPool.RegisterWaitForSingleObject(_eventWaitHandle, OnSignal, this, -1, false);

                    NativeOverlapped nol = default;
                    nol.OffsetLow = (int)(offset & uint.MaxValue);
                    nol.OffsetHigh = (int)(offset >> 32);
                    nol.EventHandle = _eventWaitHandle.SafeWaitHandle.DangerousGetHandle();

                    Marshal.StructureToPtr(nol, _overlapped, false);

                    _c = 2; // Release in callback and caller
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
                    readonly FileWaitHandler fileWaitHandler;

                    public Releaser(FileWaitHandler fileWaitHandler)
                    {
                        this.fileWaitHandler = fileWaitHandler;
                    }

                    public void Dispose()
                    {
                        fileWaitHandler.ReleaseOne();
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
                public static extern bool GetOverlappedResult(SafeFileHandle hFile, IntPtr lpOverlapped, out uint lpNumberOfBytesTransferred, bool bWait);


                [DllImport("kernel32.dll", SetLastError = true)]
                [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
                static extern bool GetFileSizeEx(SafeFileHandle hFile, out ulong size);

                internal static long? GetFileSize(SafeFileHandle handle)
                {
                    if (GetFileSizeEx(handle, out var size))
                        return (long)size;
                    else
                        return null;
                }
            }

            internal static SafeFileHandle OpenAsyncWin32(string path)
            {
                if (string.IsNullOrEmpty(path))
                    throw new ArgumentNullException(nameof(path));
#pragma warning disable CA2000 // Dispose objects before losing scope
                SafeFileHandle handle = NativeMethods.CreateFileW(path,
                    access: 0x80000000 /* GENERIC_READ */, // We want to read
                    share: 0x00000004 /* FILE_SHARE_DELETE */ | 0x00000001 /* FILE_SHARE_READ */, // Others can read, delete, rename, but we keep our file open
                    securityAttributes: IntPtr.Zero,
                    creationDisposition: 3 /* OPEN_EXISTING */,
                    0x80 /* Normal attributes */ | 0x40000000 /* FILE_FLAG_OVERLAPPED */,
                    IntPtr.Zero);
#pragma warning restore CA2000 // Dispose objects before losing scope

                if (!handle.IsInvalid)
                    return handle;
                else
                    throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()) ?? new FileNotFoundException($"Couldn't open {path}");
            }
        }
    }
}
