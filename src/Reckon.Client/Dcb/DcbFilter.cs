using Gw = Reckon.Gateway.V1;

namespace Reckon.Dcb;

/// <summary>
/// A recursive consistency-context predicate over an event's tags or type.
/// Build with the static factories and compose with <see cref="And"/> /
/// <see cref="Or"/>. Immutable.
/// </summary>
/// <remarks>
/// Maps 1:1 to reckon-proto's <c>TagFilter</c> algebra: <c>match_any</c>,
/// <c>match_all</c>, <c>event_type_match</c>, <c>conjunction</c>,
/// <c>disjunction</c>.
/// </remarks>
public sealed class DcbFilter
{
    private readonly Gw.TagFilter _wire;

    private DcbFilter(Gw.TagFilter wire) => _wire = wire;

    /// <summary>Match events carrying ANY of the given tags.</summary>
    public static DcbFilter MatchAny(params string[] tags)
    {
        RequireTags(tags);
        var list = new Gw.TagList();
        list.Tags.AddRange(tags);
        return new DcbFilter(new Gw.TagFilter { MatchAny = list });
    }

    /// <summary>Match events carrying ALL of the given tags.</summary>
    public static DcbFilter MatchAll(params string[] tags)
    {
        RequireTags(tags);
        var list = new Gw.TagList();
        list.Tags.AddRange(tags);
        return new DcbFilter(new Gw.TagFilter { MatchAll = list });
    }

    /// <summary>Match events whose type equals <paramref name="eventType"/>.</summary>
    public static DcbFilter EventType(string eventType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        return new DcbFilter(new Gw.TagFilter { EventTypeMatch = eventType });
    }

    /// <summary>Match events matching ALL sub-filters.</summary>
    public static DcbFilter And(params DcbFilter[] filters) => Compose(filters, conjunction: true);

    /// <summary>Match events matching ANY sub-filter.</summary>
    public static DcbFilter Or(params DcbFilter[] filters) => Compose(filters, conjunction: false);

    internal Gw.TagFilter ToWire() => _wire.Clone();

    private static DcbFilter Compose(DcbFilter[] filters, bool conjunction)
    {
        if (filters is null || filters.Length == 0)
        {
            throw new ArgumentException("At least one sub-filter is required.", nameof(filters));
        }

        var list = new Gw.FilterList();
        foreach (var f in filters)
        {
            ArgumentNullException.ThrowIfNull(f);
            list.Filters.Add(f._wire);
        }

        return new DcbFilter(conjunction
            ? new Gw.TagFilter { Conjunction = list }
            : new Gw.TagFilter { Disjunction = list });
    }

    private static void RequireTags(string[] tags)
    {
        if (tags is null || tags.Length == 0)
        {
            throw new ArgumentException("At least one tag is required.", nameof(tags));
        }
    }
}
