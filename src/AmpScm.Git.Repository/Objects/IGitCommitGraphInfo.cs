﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Objects
{
    interface IGitCommitGraphInfo
    {
        IEnumerable<GitId> ParentIds { get; }
        GitCommitGenerationValue Value { get; }
    }
}