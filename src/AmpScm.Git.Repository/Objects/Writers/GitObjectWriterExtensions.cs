using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Git.Objects;

namespace AmpScm.Git.Objects
{
    public static class GitObjectWriterExtensions
    {
        public static GitTreeWriter AsWriter(this GitTree tree)
        {
            if (tree is null)
                throw new ArgumentNullException(nameof(tree));

            var gtw = GitTreeWriter.CreateEmpty();

            foreach (var v in tree)
            {
                switch (v.ElementType)
                {
                    case GitTreeElementType.File:
                    case GitTreeElementType.FileExecutable:
                        gtw.Add(v.Name, ((GitBlob)v.GitObject).AsWriter());
                        break;
                    case GitTreeElementType.Directory:
                        gtw.Add(v.Name, ((GitTree)v.GitObject).AsWriter());
                        break;
                    case GitTreeElementType.SymbolicLink:
                    case GitTreeElementType.GitCommitLink:
                        throw new NotImplementedException();
                }
            }
            gtw.PutId(tree.Id); // TODO: Cleanup
            return gtw;
        }

        public static GitBlobWriter AsWriter(this GitBlob blob)
        {
            if (blob is null)
                throw new ArgumentNullException(nameof(blob));

            var bw = GitBlobWriter.CreateFrom(blob.GetBucket());

            bw.PutId(blob.Id); // TODO: Cleanup

            return bw;
        }

        public static GitCommitWriter AsWriter(this GitCommit commit)
        {
            if (commit is null)
                throw new ArgumentNullException(nameof(commit));

            var cw = GitCommitWriter.CreateFromTree(commit.Tree.AsWriter());
            cw.Parents = commit.Parents.Select(x => (x ?? throw new InvalidOperationException()).AsWriter()).ToArray();


            cw.PutId(commit.Id); // TODO: Cleanup

            return cw;
        }

        public static GitTagObjectWriter AsWriter(this GitTagObject tag)
        {
            if (tag is null)
                throw new ArgumentNullException(nameof(tag));

            var tw = GitTagObjectWriter.Create((IGitLazy<GitObject>)tag.GitObject, tag.Name!);

            tw.PutId(tag.Id);
            return tw;
        }
    }
}
