namespace DynamicDave.Umbraco.ContentHealth.Models;

/// <summary>State of a link target. Stored as int in the link table; do not renumber.</summary>
public enum LinkStatus
{
    Ok = 0,
    Missing = 1,
    Trashed = 2,
    Unpublished = 3,
}

/// <summary>Kind of link target. Stored as int in the link table; do not renumber.</summary>
public enum LinkTargetType
{
    Document = 1,
    Media = 2,
}
