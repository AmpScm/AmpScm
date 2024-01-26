using System;
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
            : base(reference, obj: null)
        {
        }

        public GitReference Reference => Named;

        public override string Name => Reference.ShortName;

        protected override GitObject GitObject => Reference.GitObject!;


        public GitTagObject? TagObject => Reference.GitObject as GitTagObject;


        /// <summary>
        /// When the Tag has a <see cref="GitTagObject"/> the tag message via <see cref="GitTagObject.Message"/>
        /// </summary>
        public string? Message => TagObject?.Message;

        /// <summary>
        /// When the Tag has a <see cref="GitTagObject"/> the tagger via <see cref="GitTagObject.Tagger"/>
        /// </summary>
        public GitSignature? Tagger => TagObject?.Tagger;
    }
}
