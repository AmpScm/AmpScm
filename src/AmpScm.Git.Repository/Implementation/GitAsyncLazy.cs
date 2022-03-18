﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Repository.Implementation
{
    internal class GitAsyncLazy<T> : Lazy<T>
    {
        public GitAsyncLazy(Func<ValueTask<T>> task) :
            base(() => task().AsTask().GetAwaiter().GetResult())
        { }
    }
}
