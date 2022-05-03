using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Repository.Implementation
{
    internal sealed class GitAsyncLazy<T> : Lazy<T>
    {
        public GitAsyncLazy(Func<ValueTask<T>> task) :
            base(() => task().AsTask().Result)
        { }

        public GitAsyncLazy(T value)
            #if !NETFRAMEWORK
            : base(value)
#else
            : base(() =>value)
#endif
        {

        }
    }
}
