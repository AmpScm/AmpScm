# AmpScm - Amplifying your Git Source Code Management
[![CI](https://github.com/AmpScm/AmpScm/actions/workflows/msbuild.yml/badge.svg)](https://github.com/AmpScm/AmpScm/actions/workflows/msbuild.yml)

This project provides a few layers of tools that allow accessing your repository from .Net without external dependencies. Unlike the libGit2 apis the code is completely managed, 100% Apache 2 Licensed, and uses many optional modern Git optimizations (E.g. commit chains, bitmap indexes, etc.) to improve performance over just scanning the repository.

## AmpScm.Buckets
[![latest version](https://img.shields.io/nuget/v/AmpScm.Buckets)](https://www.nuget.org/packages/AmpScm.Buckets)

This library provides zero-copy stream layering over different datasources, modeled like the *Apache Serf* buckets, but then completely .Net based and async enabled. When you read a bit of data the least amount of work necessary is done up the tree, and only if the highest upper layer reports a delay the task is waiting.

## AmpScm.Git.Repository
[![latest version](https://img.shields.io/nuget/v/AmpScm.Git.Repository)](https://www.nuget.org/packages/AmpScm.Git.Repository)

Completely managed Git repository level library, providing access to the repository as both *IQueryable<>* and *IAsyncQueryable<>*, to allow extending the repository walk algorithm with simple linq interaction.
  
Soon walking history should be as easy as something like:
  
```cs
// Async
using AmpScm.Git;
    
using (var repo = await GitRepository.OpenAsync(Environment.CurrentDirectory))
{
    await foreach (var r in repo.Head.Revisions)
    {
        Console.WriteLine($"commit {r.Commit.Id}");
        Console.WriteLine($"Author: {r.Commit.Author}"); // Includes timestamp
        Console.WriteLine("");
        Console.WriteLine(r.Commit.Message?.TrimEnd() + "\n");
    }
}
```

Of course you can also use the non async api if needed. This repository layer is built on top of *Amp.Buckets* via *AmpScm.Buckets.Git*, which could
be used separately if you want to write your own repository layer.

The `IAsyncQueryable<T>` support is supported via the hopefully temporary *AmpScm.Linq.AsyncQueryable*, to avoid usage conflicts between the async and non async implementations that occur when you implement both. (Let's hope this will be fixed in the BCL)
  
```cs
// Non-Async
using AmpScm.Git;
    
using (var repo = GitRepository.Open(Environment.CurrentDirectory))
{
    foreach (var r in repo.Head.Revisions)
    {
        Console.WriteLine($"commit {r.Commit.Id}");
        Console.WriteLine($"Author: {r.Commit.Author}"); // Includes timestamp
        Console.WriteLine("");
        Console.WriteLine(r.Commit.Message?.TrimEnd() + "\n");
    }
}
```
 
  
Currently this library is mostly read-only, but writing simple database entities (blob, commit, tree, tag) to the object store is supported.
  
## AmpScm.Git.Client
[![latest version](https://img.shields.io/nuget/v/AmpScm.Git.Client)](https://www.nuget.org/packages/AmpScm.Git.Client)
  
Built on top of the git repository is an early release quick and dirty Git client layer, which forwards operations to the git plumbing code. Mostly
intended for testing the lower layers, but probly useful for more users. May become a more advanced client later on.


## Git On Disk Format Support
|Feature                        | GIT        | LibGit2   | JGit    | AmpScm   |
| ----------------------------- | ---------- | --------- | ------- | -------- |
| File Blobs                    | Yes        | Yes       | Yes     | Yes      |
| Packfiles                     | Yes        | Yes       | Yes     | Yes      |
| Multipack index               | Yes        | Yes       | No      | Yes      |
| CommitGraph                   | Yes        | Yes       | No      | Yes      |
| Bitmap index Packfiles        | Yes        | No        | Yes     | Yes      |
| Bitmap index Multipack Index  | Yes        | No        | No      | Yes      |
| Reverse index Packfiles       | Yes        | No        | No      | Yes      |
| Reverse index Multipack Index | Yes        | No        | No      | Yes      |
| Directory Index format 2,3    | Yes        | Yes       | Yes     | Yes      |
| Directory Index format 4      | Yes        | Yes       | Yes     | Yes      |
| Split Directory Index format  | Yes        | No        | No      | Yes      |
| Sparse Index (Cone) support   | Yes        | No        | No      | Yes      |
