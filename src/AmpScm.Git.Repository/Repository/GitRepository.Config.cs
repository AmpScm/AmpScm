﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Signatures;
using AmpScm.Git.Repository;

namespace AmpScm.Git
{
    partial class GitRepository
    {
        internal class GitInternalConfigAccess
        {
            public GitIdType IdType { get; } = GitIdType.Sha1;

            internal GitInternalConfigAccess(GitIdType type)
            {
                IdType = type;
            }

            internal ValueTask<SignatureBucketKey?> GetKey(ReadOnlyMemory<byte> fingerprint)
            {
                return default;
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal GitInternalConfigAccess InternalConfig { get; private set;  } = new GitInternalConfigAccess(GitIdType.Sha1);

        internal void SetSHA256() // Called from repository object store on config verify
        {
            InternalConfig = new GitInternalConfigAccess(GitIdType.Sha256);
        }
    }
}
