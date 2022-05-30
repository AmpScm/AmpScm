using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using Microsoft.Win32.SafeHandles;

namespace AmpScm.Git.Objects.Writers
{
    internal static class GitInstallFile
    {
        static bool IsWindows { get; } = (Environment.OSVersion.Platform == PlatformID.Win32NT);

        public static FileStream Create(string path)
        {
            if (IsWindows)
                return NativeMethods.CreateFile(path);
            else
                return File.Create(path);
        }


        static class NativeMethods
        {
            enum FileInformationClass
            {
                FileRenameInfo = 3,
                FileDispositionInfo = 4,
            }
            [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            private static extern bool SetFileInformationByHandle(SafeHandle hFile, FileInformationClass FileInformationClass, byte[] buffer, int buffersize);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            public static extern SafeFileHandle CreateFileW(
                    [MarshalAs(UnmanagedType.LPWStr)] string filename,
                    uint access,
                    uint share,
                    IntPtr securityAttributes,
                    uint creationDisposition,
                    uint flagsAndAttributes,
                    IntPtr templateFile);

            public static bool RenameByHandle(SafeFileHandle handle, string newName)
            {
                ByteCollector bc = new(512);
                bc.Append(BitConverter.GetBytes((int)0 /* FLAGS */));
                if (IntPtr.Size > sizeof(int))
                    bc.Append(new byte[IntPtr.Size - sizeof(int)] /* Align for handle */);
                bc.Append(new byte[IntPtr.Size] /* Root-Handle */);
                bc.Append(BitConverter.GetBytes(newName.Length));
                bc.Append(Encoding.Unicode.GetBytes(newName));
                bc.Append(new byte[sizeof(char)] /* '\0' */);

                return SetFileInformationByHandle(handle, FileInformationClass.FileRenameInfo, bc.ToArray(), bc.Length);
            }

            internal static bool SetDeleteInfoByHandle(SafeFileHandle handle, bool deleteFile)
            {
                ByteCollector bc = new(4);
                bc.Append(BitConverter.GetBytes((int)(deleteFile ? 1 : 0) /* BOOL */));

                return SetFileInformationByHandle(handle, FileInformationClass.FileDispositionInfo, bc.ToArray(), bc.Length);
            }

            internal static FileStream CreateFile(string path)
            {
#pragma warning disable CA2000 // Dispose objects before losing scope
                SafeFileHandle handle = NativeMethods.CreateFileW(path,
                    access: 0x80000000 /* GENERIC_READ */ | 0x40000000 /* GENERIC_WRITE */ | 0x00010000 /* DELETE */, // We want to read
                    share: 0x00000004 /* FILE_SHARE_DELETE */ | 0x00000001 /* FILE_SHARE_READ */, // Others can read, delete, rename, but we keep our file open
                    securityAttributes: IntPtr.Zero,
                    creationDisposition: 1 /* CREATE_NEW */,
                    0x80 /* Normal attributes */,
                    IntPtr.Zero);
#pragma warning restore CA2000 // Dispose objects before losing scope

                if (!handle.IsInvalid)
                    return new FileStream(handle, FileAccess.ReadWrite);
                else
                    throw new FileNotFoundException($"Couldn't open {path}", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
                throw new NotImplementedException();
            }
        }

        internal static bool TrySetDeleteOnClose(FileStream fileStream, bool deleteFile)
        {
            if (fileStream is null)
                throw new ArgumentNullException(nameof(fileStream));

            if (IsWindows && NativeMethods.SetDeleteInfoByHandle(fileStream.SafeFileHandle, deleteFile))
                return true;

            return false;
        }

        internal static bool TryMoveFile(FileStream fileStream, string newLocation)
        {
            if (fileStream is null)
                throw new ArgumentNullException(nameof(fileStream));

            if (IsWindows && NativeMethods.RenameByHandle(fileStream.SafeFileHandle, newLocation))
                return true;

            return false;
        }
    }
}
