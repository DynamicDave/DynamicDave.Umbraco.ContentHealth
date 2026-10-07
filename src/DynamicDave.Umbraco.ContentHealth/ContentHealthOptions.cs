using System.Globalization;

namespace DynamicDave.Umbraco.ContentHealth;

/// <summary>Settings from the <c>DynamicDave:ContentHealth</c> configuration section.</summary>
public class ContentHealthOptions
{
    private static readonly string[] TimeFormats = ["HH:mm", "H:mm"];

    /// <summary>Server time of the nightly full scan, e.g. "03:00". Empty disables the nightly scan.</summary>
    public string? NightlyScanTime { get; set; } = "03:00";

    /// <summary>Aliases of document types that are not scanned as a link source.</summary>
    public string[] ExcludedDocumentTypes { get; set; } = [];

    /// <summary>Aliases of properties that are not scanned.</summary>
    public string[] ExcludedProperties { get; set; } = [];

    /// <summary>False: links to unpublished pages are not reported as a problem.</summary>
    public bool ReportUnpublishedTargets { get; set; } = true;

    /// <summary>The nightly scan time, or null when the nightly scan is disabled or the value is invalid.</summary>
    public TimeOnly? GetNightlyScanTime()
        => TimeOnly.TryParseExact(NightlyScanTime?.Trim(), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : null;

    /// <summary>True when a value is set but cannot be read; the nightly scan is then disabled.</summary>
    public bool HasInvalidNightlyScanTime => !string.IsNullOrWhiteSpace(NightlyScanTime) && GetNightlyScanTime() is null;
}
