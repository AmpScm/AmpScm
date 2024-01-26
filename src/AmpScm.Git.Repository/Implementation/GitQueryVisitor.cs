using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git.Sets;

namespace AmpScm.Git.Implementation;

internal class GitQueryVisitor : ExpressionVisitor
{
    private ConstantExpression? _defaultRoot;

    private static System.Reflection.MethodInfo GetMethod<TOn>(Expression<Func<TOn, object>> callExpression)
    {
        return (callExpression.Body as MethodCallExpression)?.Method ?? throw new ArgumentOutOfRangeException(nameof(callExpression));
    }

    // Fold all root references to exactly the same value
    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (typeof(IGitQueryRoot).IsAssignableFrom(node.Type))
        {
            if (_defaultRoot != null)
                return _defaultRoot;

            if (node.Value != null)
            {
                return _defaultRoot = node;
            }
        }
        return base.VisitConstant(node);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        for (int i = 0; i < node.Arguments.Count; i++)
        {
            var arg = node.Arguments[i];
            if (typeof(GitSet).IsAssignableFrom(arg.Type))
            {
                var paramType = node.Method.GetParameters()[0].ParameterType;

                if (IsSafeQueryableType(paramType, out var elementType))
                {
                    if (_defaultRoot == null)
                        base.VisitMethodCall(node);

                    if (_defaultRoot == null)
                        throw new InvalidOperationException();

                    var newArguments = node.Arguments.ToArray();

                    if (typeof(GitObject).IsAssignableFrom(elementType))
                        newArguments[i] = Expression.Call(_defaultRoot, GetMethod<IGitQueryRoot>(x => x.GetAll<GitObject>()).GetGenericMethodDefinition().MakeGenericMethod(elementType!));
                    else if (typeof(GitRevision).IsAssignableFrom(elementType))
                        newArguments[i] = Expression.Call(_defaultRoot, GetMethod<IGitQueryRoot>(x => x.GetRevisions(null!)), newArguments[i]);
                    else if (typeof(GitRemote).IsAssignableFrom(elementType))
                        newArguments[i] = Expression.Call(_defaultRoot, GetMethod<IGitQueryRoot>(x => x.GetAllNamed<GitRemote>()).GetGenericMethodDefinition().MakeGenericMethod(elementType!));
                    else if (typeof(GitReferenceChange).IsAssignableFrom(elementType))
                        newArguments[i] = Expression.Call(_defaultRoot, GetMethod<IGitQueryRoot>(x => x.GetAllReferenceChanges(null!)), newArguments[i]);
                    else if (typeof(IGitNamedObject).IsAssignableFrom(elementType))
                        newArguments[i] = Expression.Call(_defaultRoot, GetMethod<IGitQueryRoot>(x => x.GetAllNamed<GitReference>()).GetGenericMethodDefinition().MakeGenericMethod(elementType!));
                    else if (typeof(GitStash).IsAssignableFrom(elementType))
                        newArguments[i] = Expression.Call(_defaultRoot, GetMethod<IGitQueryRoot>(x => x.GetAllStashes(null!)), newArguments[i]);
                    else
                        throw new NotImplementedException($"Can't unwrap type {elementType}");

                    node = node.Update(node.Object!, newArguments);
                }
            }
        }

        return base.VisitMethodCall(node);
    }

    private static bool IsSafeQueryableType(Type paramType, [NotNullWhen(true)] out Type elementType)
    {
        if (GitQueryProvider.GetElementType(paramType) is Type elType)
        {
            elementType = elType;
            Type queryableType = typeof(IQueryable<>).MakeGenericType(elType);

            return paramType.IsAssignableFrom(queryableType);
        }
        elementType = null!;
        return false;
    }
}
