using System;
using FluentAssertions;
using SlateIQ.Domain.Budgeting;
using SlateIQ.Domain.Common.ValueObjects;
using Xunit;

namespace SlateIQ.Domain.Tests.Budgeting;

public class BudgetingTests
{
    [Fact]
    public void CreateBudget_InitializesInNotBaselinedState()
    {
        var orgId = Guid.NewGuid();
        var showId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var creator = Guid.NewGuid();

        var budget = Budget.Create(orgId, showId, seasonId, creator, "INR");

        budget.BudgetId.Should().NotBeEmpty();
        budget.OrgId.Should().Be(orgId);
        budget.ShowId.Should().Be(showId);
        budget.SeasonId.Should().Be(seasonId);
        budget.Status.Should().Be(BudgetStatus.NOT_BASELINED);
        budget.CurrentVersionId.Should().BeNull();
        budget.BaseCurrency.Should().Be("INR");
        budget.RowVersion.Should().Be(1);
    }

    [Fact]
    public void Baseline_FromNotBaselined_SetsCurrentVersionAndTransitionsToBaselined()
    {
        var budget = Budget.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var versionId = Guid.NewGuid();
        var user = Guid.NewGuid();

        budget.Baseline(versionId, user);

        budget.Status.Should().Be(BudgetStatus.BASELINED);
        budget.CurrentVersionId.Should().Be(versionId);
        budget.RowVersion.Should().Be(2);
    }

    [Fact]
    public void Baseline_WhenAlreadyBaselined_ThrowsInvalidOperationException()
    {
        var budget = Budget.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        budget.Baseline(Guid.NewGuid(), Guid.NewGuid());

        var act = () => budget.Baseline(Guid.NewGuid(), Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot baseline budget in state 'BASELINED'*");
    }

    [Fact]
    public void BudgetVersion_AddLine_RecalculatesTotalAmountCorrectly()
    {
        var orgId = Guid.NewGuid();
        var budgetId = Guid.NewGuid();
        var showId = Guid.NewGuid();
        var user = Guid.NewGuid();

        var version = BudgetVersion.CreateOriginalDraft(orgId, budgetId, showId, "INR", user);
        version.TotalAmount.Amount.Should().Be(0m);

        var line1 = BudgetLine.Create(
            orgId, version.BudgetVersionId, showId, Guid.NewGuid(), Guid.NewGuid(),
            "BELOW_THE_LINE", Money.From(50000.0000m, "INR"), user);
        
        var line2 = BudgetLine.Create(
            orgId, version.BudgetVersionId, showId, Guid.NewGuid(), Guid.NewGuid(),
            "ABOVE_THE_LINE", Money.From(120000.0000m, "INR"), user);

        version.AddLine(line1);
        version.AddLine(line2);
        version.SetContingency(Money.From(10000.0000m, "INR"));

        version.TotalAmount.Amount.Should().Be(180000.0000m);
        version.ContingencyAmount.Amount.Should().Be(10000.0000m);
    }

    [Fact]
    public void BudgetVersion_SubmitAndApprove_FollowsStateMachine()
    {
        var orgId = Guid.NewGuid();
        var budgetId = Guid.NewGuid();
        var showId = Guid.NewGuid();
        var user = Guid.NewGuid();

        var version = BudgetVersion.CreateOriginalDraft(orgId, budgetId, showId, "INR", user);
        var line = BudgetLine.Create(
            orgId, version.BudgetVersionId, showId, Guid.NewGuid(), Guid.NewGuid(),
            "BELOW_THE_LINE", Money.From(75000.0000m, "INR"), user);
        version.AddLine(line);

        version.Status.Should().Be(BudgetVersionStatus.DRAFT);

        version.Submit(user);
        version.Status.Should().Be(BudgetVersionStatus.SUBMITTED);
        version.SubmittedBy.Should().Be(user);

        var approver = Guid.NewGuid();
        version.Approve(approver);
        version.Status.Should().Be(BudgetVersionStatus.ACTIVE);
        version.ApprovedBy.Should().Be(approver);
    }

    [Fact]
    public void BudgetVersion_ModifyingLinesWhenNotDraft_ThrowsImmutabilityException()
    {
        var orgId = Guid.NewGuid();
        var budgetId = Guid.NewGuid();
        var showId = Guid.NewGuid();
        var user = Guid.NewGuid();

        var version = BudgetVersion.CreateOriginalDraft(orgId, budgetId, showId, "INR", user);
        var line1 = BudgetLine.Create(
            orgId, version.BudgetVersionId, showId, Guid.NewGuid(), Guid.NewGuid(),
            "BELOW_THE_LINE", Money.From(10000.0000m, "INR"), user);
        version.AddLine(line1);
        version.Submit(user);

        // Attempting to mutate in SUBMITTED state must fail per BR-BUD-003
        var line2 = BudgetLine.Create(
            orgId, version.BudgetVersionId, showId, Guid.NewGuid(), Guid.NewGuid(),
            "BELOW_THE_LINE", Money.From(5000.0000m, "INR"), user);

        var act = () => version.AddLine(line2);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Immutability invariant violation*");
    }
}
