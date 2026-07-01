using Reckon.Dcb;
using Xunit;

namespace Reckon.Client.E2E;

/// <summary>Unit tests for the DCB filter builder. No gateway required.</summary>
public sealed class DcbFilterTests
{
    [Fact]
    public void Valid_filters_compose_without_throwing()
    {
        var filter = DcbFilter.And(
            DcbFilter.MatchAny("slot:42"),
            DcbFilter.Or(
                DcbFilter.MatchAll("region:eu", "tier:gold"),
                DcbFilter.EventType("slot_reserved_v1")));

        Assert.NotNull(filter);
    }

    [Fact]
    public void MatchAny_requires_at_least_one_tag() =>
        Assert.Throws<ArgumentException>(() => DcbFilter.MatchAny());

    [Fact]
    public void MatchAll_requires_at_least_one_tag() =>
        Assert.Throws<ArgumentException>(() => DcbFilter.MatchAll());

    [Fact]
    public void And_requires_at_least_one_subfilter() =>
        Assert.Throws<ArgumentException>(() => DcbFilter.And());

    [Fact]
    public void EventType_rejects_blank() =>
        Assert.Throws<ArgumentException>(() => DcbFilter.EventType("  "));
}
