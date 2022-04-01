using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Linq
{
    public interface ISyncAndAsyncQueryProvider : IQueryProvider, IAsyncQueryProvider
    {
    }
}
