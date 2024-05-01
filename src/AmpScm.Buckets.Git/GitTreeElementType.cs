namespace AmpScm.Git;

/// <summary>
///
/// </summary>
public enum GitTreeElementType
{
    None = 0,
    /// <summary>
    /// 040000 - Directory (Id->Tree)
    /// </summary>
    Directory = 0x4000,

    /// <summary>
    /// 0100644 - File (Id->Blob)
    /// </summary>
    File = 0x81A4,
    /// <summary>
    /// 0100755 - Executable (Id->Blob)
    /// </summary>
    FileExecutable = 0x81ED,

    /// <summary>
    /// 0120000 - Symlink (Id->Blob)
    /// </summary>
    SymbolicLink = 0xA000,

    /// <summary>
    /// 0160000 - GitLink/SubModule (Id->Commit)
    /// </summary>
    GitCommitLink = 0xE000,
}
