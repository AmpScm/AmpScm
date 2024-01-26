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
    [DebuggerDisplay($"GitRepository \"{{{nameof(FullPath)},nq}}\"")]
    public partial class GitRepository : IDisposable, IGitQueryRoot, IServiceProvider
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly ServiceContainer _container;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool disposedValue;

        /// <summary>
        /// Full path of working copy root
        /// </summary>
        public string FullPath { get; }
        public bool IsBare { get; }
        public bool IsLazy => Configuration.Lazy.RepositoryIsLazy;
        public bool IsHeadDetached => Head is GitReference r && r.Resolved == r;

        public bool IsShallow => Configuration.Lazy.RepositoryIsShallow;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private GitConfiguration? _gitConfigurationLazy;

        /// <summary>
        /// The directory containing the git repository metadata (typically &lt;<see cref="FullPath"/>&gt;/.git)
        /// </summary>
        public string GitDirectory { get; }

        /// <summary>
        /// The directory containing the per worktree metadata (&lt;<see cref="FullPath"/>&gt;/.git or &lt;<see cref="GitDirectory"/>&gt;/worktrees/DIR)
        /// </summary>
        public string WorkTreeDirectory { get; }

        // Not directly creatable for now
        private GitRepository()
        {
            _container = new ServiceContainer();

            SetQueryProvider = new GitQueryProvider(this);
            Objects = new (this, () => this.Objects!);
            Commits = new (this, () => this.Commits!);
            Blobs = new (this, () => this.Blobs!);
            TagObjects = new (this, () => this.TagObjects!);
            Trees = new (this, () => this.Trees!);
            References = new (this, () => this.References!);
            Remotes = new (this, () => this.Remotes!);
            RevisionSetRoot = new (this);

            Branches = new (this, () => this.Branches!);
            Tags = new (this, () => this.Tags!);

            Stashes = new(this, () => this.Stashes!);

            ObjectRepository = null!;
            GitDirectory = default!;
            FullPath = default!;
            WorkTreeDirectory = default!;
            ReferenceRepository = null!;
            PublicKeyRepository = new(this);
        }

        internal GitRepository(string root, GitRootType rootType)
            : this()
        {
            FullPath = GitTools.GetNormalizedFullPath(root);

            bool isBare;

            if ((rootType == GitRootType.Bare || rootType == GitRootType.None)
                && FullPath.EndsWith(Path.DirectorySeparatorChar + ".git", StringComparison.OrdinalIgnoreCase))
            {
                GitDirectory = FullPath;

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
                    WorkTreeDirectory = GitDirectory = Path.Combine(FullPath, ".git");
                    break;
                case GitRootType.WorkTree:
                    {
                        string wt;
                        if (TryReadRefFile(Path.Combine(FullPath, ".git"), "gitdir: ", out var wtDir)
                            && TryReadRefFile(Path.Combine(wt = GitTools.GetNormalizedFullPath(wtDir), "commondir"), prefix: null, out var commonDir)
                            && Directory.Exists(GitDirectory = Path.Combine(wt, commonDir))
                            && File.Exists(Path.Combine(GitDirectory, "config")))
                        {
                            GitDirectory = GitTools.GetNormalizedFullPath(GitDirectory);
                            WorkTreeDirectory = wt;
                        }
                        else
                            throw new GitRepositoryException($"Unable to read WorkTree configuration for '{FullPath}");
                        break;
                    }
                case GitRootType.Bare:
                    WorkTreeDirectory = GitDirectory = FullPath;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(rootType));
            }

            ObjectRepository = new Objects.GitRepositoryObjectRepository(this, Path.Combine(GitDirectory, "objects"));
            ReferenceRepository = new References.GitRepositoryReferenceRepository(this, GitDirectory, WorkTreeDirectory);
        }

        public GitObjectSet<GitObject> Objects { get; }
        public GitObjectSet<GitCommit> Commits { get; }
        public GitObjectSet<GitTree> Trees { get; }
        public GitObjectSet<GitBlob> Blobs { get; }
        public GitObjectSet<GitTagObject> TagObjects { get; }

        public GitNamedSet<GitBranch> Branches { get; }

        public GitNamedSet<GitTag> Tags { get; }

        public GitStashSet Stashes { get; }

        public GitReferencesSet References { get; }
        public GitRemotesSet Remotes { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal GitRevisionSet RevisionSetRoot { get; }

        public GitConfiguration Configuration => _gitConfigurationLazy ??= new GitConfiguration(this, GitDirectory);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal GitQueryProvider SetQueryProvider { get; }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public Objects.GitObjectRepository ObjectRepository { get; }
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public References.GitReferenceRepository ReferenceRepository { get; }
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public Objects.GitPublicKeyRepository PublicKeyRepository { get; }

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
            where TResult : class => SetQueryProvider.GetAll<TResult>();

        IQueryable<TResult> IGitQueryRoot.GetAllNamed<TResult>()
            where TResult : class => SetQueryProvider.GetAllNamed<TResult>();

        ValueTask<TResult?> IGitQueryRoot.GetByIdAsync<TResult>(GitId id)
            where TResult : class => SetQueryProvider.GetByIdAsync<TResult>(id);

        ValueTask<TResult?> IGitQueryRoot.GetNamedAsync<TResult>(string name)
            where TResult : class => SetQueryProvider.GetNamedAsync<TResult>(name);

        internal ValueTask<TResult?> GetAsync<TResult>(GitId id)
            where TResult : GitObject => SetQueryProvider.GetByIdAsync<TResult>(id);

        IQueryable<GitRevision> IGitQueryRoot.GetRevisions(GitRevisionSet set) => SetQueryProvider.GetRevisions(set);

        IQueryable<GitReferenceChange> IGitQueryRoot.GetAllReferenceChanges(GitReferenceChangeSet set) => SetQueryProvider.GetAllReferenceChanges(set);

        IQueryable<GitStash> IGitQueryRoot.GetAllStashes(GitStashSet set) => SetQueryProvider.GetAllStashes(set);

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
                return $"[Bare Repository] GitDir={GitDirectory}";
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
