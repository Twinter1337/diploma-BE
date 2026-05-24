namespace CaoachlyBE.Helpers;

internal static class UaTime
{
    // Ukraine has been permanently on UTC+3 (EEST) since October 2022.
    private static readonly TimeZoneInfo Zone =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");

    public static DateTime Now =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

    // Converts any DateTime (UTC or with offset) to Kyiv local time for comparisons
    public static DateTime FromUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    // Converts an inbound DateTime (any Kind) to Kyiv local time.
    // Unspecified is treated as UTC; Local is converted via the server's tz.
    public static DateTime ToKyiv(DateTime dt) => dt.Kind switch
    {
        DateTimeKind.Utc => TimeZoneInfo.ConvertTimeFromUtc(dt, Zone),
        DateTimeKind.Local => TimeZoneInfo.ConvertTime(dt, Zone),
        _ => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(dt, DateTimeKind.Utc), Zone)
    };

    // Converts a DateTimeOffset to Kyiv local time, preserving the absolute moment.
    public static DateTime ToKyiv(DateTimeOffset dto) =>
        TimeZoneInfo.ConvertTime(dto, Zone).DateTime;
}
