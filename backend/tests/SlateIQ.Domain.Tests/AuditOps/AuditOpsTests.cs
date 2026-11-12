using System;
using FluentAssertions;
using SlateIQ.Domain.AuditOps;
using Xunit;

namespace SlateIQ.Domain.Tests.AuditOps;

public class AuditOpsTests
{
    [Fact]
    public void AuditAssignment_FollowsStateMachine()
    {
        var orgId = Guid.NewGuid();
        var showId = Guid.NewGuid();
        var auditorId = Guid.NewGuid();
        var creator = Guid.NewGuid();

        var assignment = AuditAssignment.Create(
            orgId: orgId,
            showId: showId,
            auditorUserId: auditorId,
            assignmentType: AssignmentType.INTERNAL,
            startDate: new DateOnly(2026, 12, 1),
            endDate: new DateOnly(2027, 3, 31),
            permissionLevel: PermissionLevel.REVIEW,
            createdBy: creator);

        assignment.Status.Should().Be(AssignmentStatus.PLANNED);

        assignment.Activate(creator);
        assignment.Status.Should().Be(AssignmentStatus.ACTIVE);

        assignment.Complete(creator);
        assignment.Status.Should().Be(AssignmentStatus.COMPLETED);
        assignment.HandedOverAt.Should().NotBeNull();
    }

    [Fact]
    public void AuditAssignment_WithInvertedDates_ThrowsArgumentException()
    {
        var act = () => AuditAssignment.Create(
            orgId: Guid.NewGuid(),
            showId: Guid.NewGuid(),
            auditorUserId: Guid.NewGuid(),
            assignmentType: AssignmentType.INTERNAL,
            startDate: new DateOnly(2027, 1, 1),
            endDate: new DateOnly(2026, 1, 1),
            permissionLevel: PermissionLevel.READ_ONLY,
            createdBy: Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*End date cannot precede start date*");
    }

    [Fact]
    public void AuditFinding_FollowsStateMachine()
    {
        var orgId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var showId = Guid.NewGuid();
        var creator = Guid.NewGuid();

        var finding = AuditFinding.Create(
            orgId: orgId,
            auditAssignmentId: assignmentId,
            showId: showId,
            severity: FindingSeverity.HIGH,
            category: "PROCUREMENT_BREACH",
            title: "Missing 3 quotes for heavy camera crane rental",
            description: "Invoice exceeds threshold but lacks competitive tender quotes.",
            createdBy: creator);

        finding.Status.Should().Be(FindingStatus.DRAFT);

        finding.Open(creator);
        finding.Status.Should().Be(FindingStatus.OPEN);

        finding.Resolve(creator);
        finding.Status.Should().Be(FindingStatus.RESOLVED);

        finding.Close("Line producer provided historical master rate contract", creator);
        finding.Status.Should().Be(FindingStatus.CLOSED);
        finding.ClosureReason.Should().Be("Line producer provided historical master rate contract");
    }
}
