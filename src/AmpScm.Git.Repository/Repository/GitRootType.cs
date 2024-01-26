using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Repository;

internal enum GitRootType
{
    None,
    Normal,
    Bare,
    WorkTree
}
