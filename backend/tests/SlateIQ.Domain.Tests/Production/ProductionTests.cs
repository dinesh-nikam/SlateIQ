using System;
using FluentAssertions;
using SlateIQ.Domain.Production;
using Xunit;

namespace SlateIQ.Domain.Tests.Production;

public class ProductionTests
{
    [Fact]
    public void CreateShow_WithValidParameters_InitializesInPlanningState()
    {
        var orgId = Guid.NewGuid();
        var creator = Guid.NewGuid();

        var show = Show.Create(
            orgId: orgId,
            code: "MNS01",
            title: "Monsoon Wedding Season 1",
            showType: ShowType.SERIES,
            createdBy: creator,
            format: "4K HDR",
            genre: "Drama",
            plannedStartDate: new DateOnly(2026, 12, 1),
            plannedEndDate: new DateOnly(2027, 4, 30),
            plannedShootDays: 60);

        show.ShowId.Should().NotBeEmpty();
        show.OrgId.Should().Be(orgId);
        show.Code.Should().Be("MNS01");
        show.Title.Should().Be("Monsoon Wedding Season 1");
        show.ShowType.Should().Be(ShowType.SERIES);
        show.Status.Should().Be(ShowStatus.PLANNING);
        show.HostingPattern.Should().Be(HostingPattern.H1);
        show.RowVersion.Should().Be(1);
    }

    [Theory]
    [InlineData("mns01")] // Lowercase not allowed (A2-05)
    [InlineData("M1")]    // Too short (min 3)
    [InlineData("TOOLONGA123456")] // Too long (max 12)
    [InlineData("MNS-01")] // Hyphens not allowed in code
    public void CreateShow_WithInvalidCode_ThrowsArgumentException(string invalidCode)
    {
        var act = () => Show.Create(
            orgId: Guid.NewGuid(),
            code: invalidCode,
            title: "Invalid Show",
            showType: ShowType.SERIES,
            createdBy: Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*3-12 alphanumeric uppercase*");
    }

    [Fact]
    public void CreateShow_WithEndDateBeforeStartDate_ThrowsArgumentException()
    {
        var act = () => Show.Create(
            orgId: Guid.NewGuid(),
            code: "MNS02",
            title: "Invalid Date Show",
            showType: ShowType.FILM,
            createdBy: Guid.NewGuid(),
            plannedStartDate: new DateOnly(2026, 12, 31),
            plannedEndDate: new DateOnly(2026, 1, 1));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Planned end date cannot precede planned start date*");
    }

    [Fact]
    public void Activate_FromPlanningState_TransitionsToActiveAndIncrementsVersion()
    {
        var show = Show.Create(Guid.NewGuid(), "MNS03", "Valid Show", ShowType.SERIES, Guid.NewGuid());
        var user = Guid.NewGuid();

        show.Activate(user);

        show.Status.Should().Be(ShowStatus.ACTIVE);
        show.UpdatedBy.Should().Be(user);
        show.RowVersion.Should().Be(2);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ThrowsInvalidOperationException()
    {
        var show = Show.Create(Guid.NewGuid(), "MNS04", "Valid Show", ShowType.SERIES, Guid.NewGuid());
        show.Activate(Guid.NewGuid());

        var act = () => show.Activate(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot activate show in state 'ACTIVE'*");
    }

    [Fact]
    public void WrapAndResume_TransitionsCorrectly()
    {
        var show = Show.Create(Guid.NewGuid(), "MNS05", "Valid Show", ShowType.SERIES, Guid.NewGuid());
        show.Activate(Guid.NewGuid());

        show.Wrap(Guid.NewGuid());
        show.Status.Should().Be(ShowStatus.COMPLETED);

        show.Resume(Guid.NewGuid());
        show.Status.Should().Be(ShowStatus.ACTIVE);
    }

    [Fact]
    public void Cancel_FromPlanningWithReason_TransitionsToCancelled()
    {
        var show = Show.Create(Guid.NewGuid(), "MNS06", "Valid Show", ShowType.SERIES, Guid.NewGuid());
        var user = Guid.NewGuid();

        show.Cancel("Project shelved due to financing withdrawal", user);

        show.Status.Should().Be(ShowStatus.CANCELLED);
        show.CancelReason.Should().Be("Project shelved due to financing withdrawal");
        show.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_WhenActive_ThrowsInvalidOperationException()
    {
        var show = Show.Create(Guid.NewGuid(), "MNS07", "Valid Show", ShowType.SERIES, Guid.NewGuid());
        show.Activate(Guid.NewGuid());

        var act = () => show.Cancel("Late cancellation", Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Shows can only be cancelled from PLANNING*");
    }
}
