namespace AmpScm.Git;

public enum GitObjectType
{
    None = 0, // Reserved. Unused

    // These types are valid objects
    Commit = 1,
    Tree = 2,
    Blob = 3,
    Tag = 4,
};
