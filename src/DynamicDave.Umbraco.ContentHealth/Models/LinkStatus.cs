using System.Text.Json.Serialization;

namespace DynamicDave.Umbraco.ContentHealth.Models;

/// <summary>State of a link target. Stored as int in the link table; do not renumber.</summary>
// Package controllers do not use Umbraco's backoffice JSON options, so enums declare their own string converter.
[JsonConverter(typeof(JsonStringEnumConverter<LinkStatus>))]
public enum LinkStatus
{
    Ok = 0,
    Missing = 1,
    Trashed = 2,
    Unpublished = 3,
}

/// <summary>Kind of link target. Stored as int in the link table; do not renumber.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LinkTargetType>))]
public enum LinkTargetType
{
    Document = 1,
    Media = 2,
}
