using System.Linq.Expressions;
using AmpScm.Git.References;

namespace AmpScm.Git.Sets;

public class GitReferencesSet : GitNamedSet<GitReference>
{
    internal GitReferencesSet(GitRepository repository, Expression<Func<GitNamedSet<GitReference>>> rootExpression)
        : base(repository, rootExpression)
    {
    }

    public GitReference Head => Repository.ReferenceRepository.GetAsync(GitReferenceRepository.Head).AsTask().Result!;



    public GitReferenceUpdateTransaction CreateUpdateTransaction()
    {
        return Repository.ReferenceRepository.CreateUpdateTransaction();
    }
}
