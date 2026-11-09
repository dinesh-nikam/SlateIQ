using System;
using FluentAssertions;
using SlateIQ.Domain.Authorization;
using SlateIQ.Domain.Common.ValueObjects;
using Xunit;

namespace SlateIQ.Domain.Tests.Authorization;

public class AuthorizationTests
{
    [Fact]
    public void ScopeHierarchy_OrgEncompassesAllLowerScopes()
    {
        ScopeType.ORG.Encompasses(ScopeType.SHOW).Should().BeTrue();
        ScopeType.ORG.Encompasses(ScopeType.DEPARTMENT).Should().BeTrue();
        ScopeType.ORG.Encompasses(ScopeType.SELF).Should().BeTrue();
        
        ScopeType.DEPARTMENT.Encompasses(ScopeType.ORG).Should().BeFalse();
        ScopeType.SHOW.Encompasses(ScopeType.DEPARTMENT).Should().BeTrue();
    }

    [Fact]
    public void PermissionKey_Format_ParsesCorrectly()
    {
        var key = PermissionKey.Parse("expense.submission.submit");
        key.Domain.Should().Be("expense");
        key.Resource.Should().Be("submission");
        key.Action.Should().Be("submit");
        key.Value.Should().Be("expense.submission.submit");
    }

    [Fact]
    public void PermissionKey_InvalidFormat_ThrowsArgumentException()
    {
        Action act = () => PermissionKey.Parse("invalid_key_without_periods");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SoD_SubmitterCannotApproveOwnExpense_TS09()
    {
        var submitterId = Guid.NewGuid();
        var approverId = submitterId; // Same user

        Action act = () => SeparationOfDutiesValidator.AssertSubmitterIsNotApprover(submitterId, approverId);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SELF_APPROVAL_PROHIBITED*");
    }

    [Fact]
    public void SoD_IndependentApprover_Succeeds()
    {
        var submitterId = Guid.NewGuid();
        var approverId = Guid.NewGuid(); // Different user

        Action act = () => SeparationOfDutiesValidator.AssertSubmitterIsNotApprover(submitterId, approverId);

        act.Should().NotThrow();
    }

    [Fact]
    public void SoD_ApproverLimitExceeded_ThrowsException_TS10()
    {
        var expenseAmount = new Money(150000.0000m, "INR");
        var approverLimit = new Money(100000.0000m, "INR");

        Action act = () => SeparationOfDutiesValidator.AssertWithinApprovalLimit(expenseAmount, approverLimit);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*APPROVAL_LIMIT_EXCEEDED*");
    }

    [Fact]
    public void SoD_ApproverLimitWithinCeiling_Succeeds()
    {
        var expenseAmount = new Money(50000.0000m, "INR");
        var approverLimit = new Money(100000.0000m, "INR");

        Action act = () => SeparationOfDutiesValidator.AssertWithinApprovalLimit(expenseAmount, approverLimit);

        act.Should().NotThrow();
    }
}
