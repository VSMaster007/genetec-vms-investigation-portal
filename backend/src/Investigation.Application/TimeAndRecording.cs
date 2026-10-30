using System.Globalization;
using System.Text.RegularExpressions;
using Investigation.Domain;

namespace Investigation.Application;

public static partial class TimestampNormalizer
{
    public static DateTime ParseUtc(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new TimestampNormalizationException("A timestamp is required.");
        }

        var trimmed = value.Trim();
        if (!OffsetPattern().IsMatch(trimmed))
        {
            throw new TimestampNormalizationException(
                "Timestamps must be UTC or include a numeric offset. Example: 2026-10-15T14:35:20Z.");
        }

        if (!DateTimeOffset.TryParse(
                trimmed,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            throw new TimestampNormalizationException($"'{trimmed}' is not a valid timestamp.");
        }

        return DateTime.SpecifyKind(parsed.UtcDateTime, DateTimeKind.Utc);
    }

    public static string FormatUtc(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        return utc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"(Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex OffsetPattern();
}

public readonly record struct PlaybackWindow(DateTime StartUtc, DateTime EventUtc, DateTime EndUtc);

public static class PlaybackWindowCalculator
{
    public static PlaybackWindow Create(DateTime eventTimestampUtc, int preEventSeconds, int postEventSeconds, int maxWindowSeconds)
    {
        if (eventTimestampUtc.Kind != DateTimeKind.Utc)
        {
            throw new TimestampNormalizationException("Incident timestamps must be stored as UTC before a playback window is calculated.");
        }

        if (preEventSeconds < 0 || postEventSeconds < 0)
        {
            throw new TimestampNormalizationException("The playback window cannot use a negative offset.");
        }

        if (preEventSeconds + postEventSeconds > maxWindowSeconds)
        {
            throw new TimestampNormalizationException(
                $"The playback window cannot exceed {maxWindowSeconds} seconds.");
        }

        return new PlaybackWindow(
            eventTimestampUtc.AddSeconds(-preEventSeconds),
            eventTimestampUtc,
            eventTimestampUtc.AddSeconds(postEventSeconds));
    }
}

public sealed record TimeRange(DateTime StartUtc, DateTime EndUtc);

public sealed record RecordingAssessment(
    string State,
    bool CoversEvent,
    bool CanAttemptPlayback,
    string Message,
    IReadOnlyList<TimeRange> Sequences,
    IReadOnlyList<TimeRange> Gaps);

public static class RecordingWindowAnalyzer
{
    public static RecordingAssessment Assess(
        DateTime windowStartUtc,
        DateTime eventUtc,
        DateTime windowEndUtc,
        DateTime utcNow,
        TimeSpan? retention,
        bool cameraFound,
        bool accessGranted,
        bool querySucceeded,
        string? queryError,
        IReadOnlyList<TimeRange> sequences)
    {
        if (!cameraFound)
        {
            return Failed("CameraNotFound", "The camera was not returned by the Genetec integration.");
        }

        if (!accessGranted)
        {
            return Failed("InsufficientPermissions", "Playback permission was denied for this camera.");
        }

        if (!querySucceeded)
        {
            return Failed(string.IsNullOrWhiteSpace(queryError) ? "RecordingQueryFailed" : queryError, "Archived recording availability could not be verified.");
        }

        if (retention is { } configuredRetention && eventUtc < utcNow - configuredRetention)
        {
            return new RecordingAssessment(
                "RetentionExpired",
                false,
                false,
                "The incident is older than the recording retention period for this camera.",
                Array.Empty<TimeRange>(),
                Array.Empty<TimeRange>());
        }

        var merged = Merge(sequences);
        var overlapping = merged
            .Where(sequence => sequence.EndUtc > windowStartUtc && sequence.StartUtc < windowEndUtc)
            .Select(sequence => new TimeRange(
                sequence.StartUtc < windowStartUtc ? windowStartUtc : sequence.StartUtc,
                sequence.EndUtc > windowEndUtc ? windowEndUtc : sequence.EndUtc))
            .ToArray();

        var gaps = Gaps(windowStartUtc, windowEndUtc, overlapping);
        var coversEvent = overlapping.Any(sequence => sequence.StartUtc <= eventUtc && eventUtc < sequence.EndUtc);

        if (overlapping.Length == 0)
        {
            return new RecordingAssessment(
                "Unavailable",
                false,
                false,
                "No archived recording covers this playback window.",
                overlapping,
                gaps);
        }

        if (!coversEvent)
        {
            return new RecordingAssessment(
                "Gap",
                false,
                false,
                "The incident timestamp falls in a gap in the archived recording.",
                overlapping,
                gaps);
        }

        if (gaps.Count > 0)
        {
            return new RecordingAssessment(
                "Partial",
                true,
                true,
                "Archived video covers the incident timestamp. The playback window also contains gaps.",
                overlapping,
                gaps);
        }

        return new RecordingAssessment(
            "Available",
            true,
            true,
            "Archived video covers the incident timestamp.",
            overlapping,
            gaps);
    }

    private static RecordingAssessment Failed(string state, string message) =>
        new(state, false, false, message, Array.Empty<TimeRange>(), Array.Empty<TimeRange>());

    public static IReadOnlyList<TimeRange> Merge(IReadOnlyList<TimeRange> sequences)
    {
        var ordered = sequences
            .Where(sequence => sequence.EndUtc > sequence.StartUtc)
            .OrderBy(sequence => sequence.StartUtc)
            .ToList();

        if (ordered.Count == 0)
        {
            return Array.Empty<TimeRange>();
        }

        var merged = new List<TimeRange> { ordered[0] };
        for (var index = 1; index < ordered.Count; index++)
        {
            var current = ordered[index];
            var previous = merged[^1];
            if (current.StartUtc <= previous.EndUtc)
            {
                merged[^1] = new TimeRange(previous.StartUtc, current.EndUtc > previous.EndUtc ? current.EndUtc : previous.EndUtc);
            }
            else
            {
                merged.Add(current);
            }
        }

        return merged;
    }

    public static IReadOnlyList<TimeRange> Gaps(DateTime windowStartUtc, DateTime windowEndUtc, IReadOnlyList<TimeRange> coverage)
    {
        var gaps = new List<TimeRange>();
        var cursor = windowStartUtc;
        foreach (var sequence in coverage.OrderBy(sequence => sequence.StartUtc))
        {
            if (sequence.StartUtc > cursor)
            {
                gaps.Add(new TimeRange(cursor, sequence.StartUtc));
            }

            if (sequence.EndUtc > cursor)
            {
                cursor = sequence.EndUtc;
            }
        }

        if (cursor < windowEndUtc)
        {
            gaps.Add(new TimeRange(cursor, windowEndUtc));
        }

        return gaps;
    }
}
