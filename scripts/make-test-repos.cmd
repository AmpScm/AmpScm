@echo off
setlocal enableextensions
if "%1" == "--clean" (
  for /d %%1 in (*-*) do rmdir /s /q %%1
)

if NOT EXIST git-depth-1              @git clone https://github.com/git/git.git --depth=1                   git-depth-1
if NOT EXIST git-no-blob              @git clone https://github.com/git/git.git --filter=blob:none          git-no-blob
if NOT EXIST git-no-blob-alt-cs       @git clone -s git-no-blob                                             git-no-blob-alt-cs
if NOT EXIST libgit2-std              @git clone https://github.com/libgit2/libgit2.git                     libgit2-std
if NOT EXIST libgit2-bmp              @git clone libgit2-std                                                libgit2-bmp
if NOT EXIST lin-no-tree-cs           @git clone https://github.com/torvalds/linux.git --filter=tree:0      lin-no-tree-cs
REM if NOT EXIST git-reftable             @git clone https://github.com/git/git.git --filter=tree:0             git-reftable

for /d %%1 in (*-cs) do (
  if NOT EXIST %%1\.git\objects\info\commit-graph (
    pushd %%1
    git commit-graph write
    popd
  )
)

pushd libgit2-bmp
git repack -a -b -d
popd
