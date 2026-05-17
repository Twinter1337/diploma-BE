namespace CaoachlyBE.Helpers;

internal static class UaTime
{
    // Ukraine has been permanently on UTC+3 (EEST) since October 2022.
    private static readonly TimeZoneInfo Zone =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Kiev");

    public static DateTime Now =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);
}
