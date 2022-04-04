using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;
using AmpScm.Git.Implementation;
using AmpScm.Git.Repository;
using AmpScm.Git.Sets;

[assembly: CLSCompliant(true)]

namespace AmpScm.Git
{
    [DebuggerDisplay("GitRepository {GitDir}")]
    public partial class GitRepository : IDisposable, IGitQueryRoot, IServiceProvider
    {
        readonly ServiceContainer _container;
        private bool disposedValue;
        public string FullPath { get; }
        public bool IsBare { get; }
        public bool IsLazy => Configuration.Lazy.RepositoryIsLazy;
        public bool IsShallow => Configuration.Lazy.RepositoryIsShallow;
        readonly Lazy<GitConfiguration> _gitConfiguration;

        protected internal string GitDir { get; }
        protected internal string WorkTreeDir { get; }

        // Not directly creatable for now
        private GitRepository()
        {
            _container = new ServiceContainer();

            SetQueryProvider = new GitQueryProvider(this);
            Objects = new GitObjectSet<GitObject>(this, () => this.Objects!);
            Commits = new GitCommitsSet(this, () => this.Commits!);
            Blobs = new GitObjectSet<GitBlob>(this, () => this.Blobs!);
            TagObjects = new GitObjectSet<GitTagObject>(this, () => this.TagObjects!);
            Trees = new GitObjectSet<GitTree>(this, () => this.Trees!);
            References = new GitReferencesSet(this, () => this.References!);
            Remotes = new GitRemotesSet(this, () => this.Remotes!);
            _gitConfiguration = new Lazy<GitConfiguration>(LoadConfig);
            NoRevisions = new GitRevisionSet(this);

            Branches = new GitBranchesSet(this, () => this.Branches!);
            Tags = new GitTagsSet(this, () => this.Tags!);

            ObjectRepository = null!;
            GitDir = default!;
            FullPath = default!;
            WorkTreeDir = default!;
            ReferenceRepository = null!;
        }

        internal GitRepository(string root, GitRootType rootType)
            : this()
        {
            FullPath = GitTools.GetNormalizedFullPath(root);

            bool isBare;

            if ((rootType == GitRootType.Bare || rootType == GitRootType.None)
                && FullPath.EndsWith(Path.DirectorySeparatorChar + ".git", StringComparison.OrdinalIgnoreCase))
            {
                GitDir = FullPath;

                if (!(Configuration?.GetBool("core", "bare") ?? false))
                {
                    isBare = false;
                    rootType = GitRootType.Normal;
                    FullPath = Path.GetDirectoryName(FullPath) ?? throw new InvalidOperationException();
                }
                else
                    isBare = true;
            }
            else
                isBare = (rootType == GitRootType.Bare);

            IsBare = isBare;

            switch (rootType)
            {
                case GitRootType.Normal:
                case GitRootType.None:
                    WorkTreeDir = GitDir = Path.Combine(FullPath, ".git");
                    break;
                case GitRootType.WorkTree:
                    {
                        string wt;
                        if (TryReadRefFile(Path.Combine(FullPath, ".git"), "gitdir: ", out var wtDir)
                            && TryReadRefFile(Path.Combine(wt = GitTools.GetNormalizedFullPath(wtDir), "commondir"), null, out var commonDir)
                            && Directory.Exists(GitDir = Path.Combine(wt, commonDir))
                            && File.Exists(Path.Combine(GitDir, "config")))
                        {
                            GitDir = GitTools.GetNormalizedFullPath(GitDir);
                            WorkTreeDir = wt;
                        }
                        else
                            throw new GitRepositoryException($"Unable to read WorkTree configuration for '{FullPath}");
                        break;
                    }
                case GitRootType.Bare:
                    WorkTreeDir = GitDir = FullPath;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(rootType));
            }

            ObjectRepository = new Objects.GitRepositoryObjectRepository(this, Path.Combine(GitDir, "objects"));
            ReferenceRepository = new References.GitRepositoryReferenceRepository(this, GitDir, WorkTreeDir);
        }

        public GitObjectSet<GitObject> Objects { get; }
        public GitCommitsSet Commits { get; }
        public GitObjectSet<GitTree> Trees { get; }
        public GitObjectSet<GitBlob> Blobs { get; }
        public GitObjectSet<GitTagObject> TagObjects { get; }

        public GitNamedSet<GitBranch> Branches { get; }

        public GitNamedSet<GitTag> Tags { get; }

        public GitReferencesSet References { get; }
        public GitRemotesSet Remotes { get; }

        internal GitRevisionSet NoRevisions { get; }

        public GitConfiguration Configuration => _gitConfiguration.Value;

        internal GitQueryProvider SetQueryProvider { get; }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public Objects.GitObjectRepository ObjectRepository { get; }
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public References.GitReferenceRepository ReferenceRepository { get; }

        public GitReference Head => References.Head;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    ObjectRepository.Dispose();
                    _container.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        IQueryable<TResult> IGitQueryRoot.GetAll<TResult>()
            where TResult : class
        {
            return SetQueryProvider.GetAll<TResult>();
        }

        IQueryable<TResult> IGitQueryRoot.GetAllNamed<TResult>()
            where TResult : class
        {
            return SetQueryProvider.GetAllNamed<TResult>();
        }

        ValueTask<TResult?> IGitQueryRoot.GetByIdAsync<TResult>(GitId id)
            where TResult : class
        {
            return SetQueryProvider.GetByIdAsync<TResult>(id);
        }

        ValueTask<TResult?> IGitQueryRoot.GetNamedAsync<TResult>(string name)
            where TResult : class
        {
            return SetQueryProvider.GetNamedAsync<TResult>(name);
        }

        internal ValueTask<TResult?> GetAsync<TResult>(GitId id)
            where TResult : GitObject
        {
            return SetQueryProvider.GetByIdAsync<TResult>(id);
        }

        IQueryable<GitRevision> IGitQueryRoot.GetRevisions(GitRevisionSet set)
        {
            return SetQueryProvider.GetRevisions(set);
        }

        IQueryable<GitReferenceChange> IGitQueryRoot.GetAllReferenceChanges(GitReferenceChangeSet set)
        {
            return SetQueryProvider.GetAllReferenceChanges(set);
        }

        object? IServiceProvider.GetService(Type serviceType)
        {
            return GetService(serviceType);
        }

        protected virtual object? GetService(Type serviceType)
        {
            return ((IServiceProvider)_container).GetService(serviceType);
        }

        public override string ToString()
        {
            if (IsBare)
                return $"[Bare Repository] GitDir={GitDir}";
            else
                return $"[Git Repository] FullPath={FullPath}";
        }

        internal T? GetService<T>()
            where T : class
        {
            return _container.GetService(typeof(T)) as T;
        }
    }
}
