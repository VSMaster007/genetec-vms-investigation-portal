using Investigation.Domain;

namespace Investigation.Application;

public sealed record DemoTimeline(
    DateTime RecordingStartUtc,
    DateTime RecordingEndUtc,
    double DurationSeconds,
    double IncidentOffsetSeconds,
    double PlaybackStartOffsetSeconds,
    double PlaybackEndOffsetSeconds,
    double SeekOffsetSeconds);

public static class DemoTimestampMapper
{
    public static DemoTimeline? Map(
        DateTime recordingStartUtc,
        double durationSeconds,
        DateTime incidentUtc,
        int preEventSeconds,
        int postEventSeconds)
    {
        if (durationSeconds <= 0
            || recordingStartUtc.Kind != DateTimeKind.Utc
            || incidentUtc.Kind != DateTimeKind.Utc
            || preEventSeconds < 0
            || postEventSeconds < 0)
        {
            return null;
        }

        var incidentOffset = (incidentUtc - recordingStartUtc).TotalSeconds;
        if (incidentOffset < 0 || incidentOffset > durationSeconds)
        {
            return null;
        }

        return new DemoTimeline(
            recordingStartUtc,
            recordingStartUtc.AddSeconds(durationSeconds),
            durationSeconds,
            incidentOffset,
            Math.Max(0, incidentOffset - preEventSeconds),
            Math.Min(durationSeconds, incidentOffset + postEventSeconds),
            incidentOffset);
    }

    public static DateTime UtcAtOffset(DateTime recordingStartUtc, double offsetSeconds) =>
        DateTime.SpecifyKind(recordingStartUtc.AddSeconds(offsetSeconds), DateTimeKind.Utc);

    public static double OffsetAtUtc(DateTime recordingStartUtc, DateTime instantUtc) =>
        (instantUtc - recordingStartUtc).TotalSeconds;

    public static DateTime RecordingStartForAnchor(DateTime anchorUtc, double incidentOffsetSeconds)
    {
        if (anchorUtc.Kind != DateTimeKind.Utc || incidentOffsetSeconds < 0)
        {
            throw new TimestampNormalizationException("Demo recording start requires a UTC anchor and a non-negative media offset.");
        }

        return DateTime.SpecifyKind(anchorUtc.AddSeconds(-incidentOffsetSeconds), DateTimeKind.Utc);
    }
}
