namespace DynamicDave.Umbraco.ContentHealth.Queries;

/// <summary>Paging limits shared by the API endpoints that return pages.</summary>
public static class Paging
{
    public const int MaxTake = 100;

    public static bool IsValid(int skip, int take) => skip >= 0 && take >= 1 && take <= MaxTake;
}
