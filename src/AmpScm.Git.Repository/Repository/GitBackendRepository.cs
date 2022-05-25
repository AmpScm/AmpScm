using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Repository
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public abstract class GitBackendRepository : IDisposable
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected internal GitRepository Repository { get; }

        protected GitBackendRepository(GitRepository repository)
        {
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        protected abstract void Dispose(bool disposing);

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                string name = GetType().Name;

                if (name.StartsWith("Git", StringComparison.Ordinal))
                    name = name.Substring("Git".Length);
                if (name.EndsWith("Repository", StringComparison.Ordinal))
                    name = name.Substring(0, name.Length - "Repository".Length);

                return $"{name} Repository";
            }
        }
    }
}
