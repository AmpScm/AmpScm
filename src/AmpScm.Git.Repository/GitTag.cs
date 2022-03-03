﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git.Sets;

namespace AmpScm.Git
{
    public sealed class GitTag : GitNamedObjectWrapper<GitObject, GitReference>
    {
        internal GitTag(GitReference reference)
            : base(reference, null)
        {
        }

        public GitReference Reference => Named;

        public override string Name => Reference.ShortName;

        protected override GitObject Object => Reference.Object!;
    }
}
