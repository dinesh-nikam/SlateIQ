using System;
using FluentAssertions;
using SlateIQ.Domain.Common.ValueObjects;
using SlateIQ.Domain.Vendors;
using Xunit;

namespace SlateIQ.Domain.Tests.Vendors;

public class VendorTests
{
    [Fact]
    public void CreateVendor_InitializesInCandidateState()
    {
        var orgId = Guid.NewGuid();
        var creator = Guid.NewGuid();

        var vendor = Vendor.Create(
            orgId: orgId,
            code: "VND-ARRI",
            legalName: "ARRI India Equipment Pvt Ltd",
            vendorType: VendorType.COMPANY,
            createdBy: creator,
            primaryTaxIdentifier: "27AABCA1234F1Z5");

        vendor.VendorId.Should().NotBeEmpty();
        vendor.OrgId.Should().Be(orgId);
        vendor.Code.Should().Be("VND-ARRI");
        vendor.LegalName.Should().Be("ARRI India Equipment Pvt Ltd");
        vendor.NormalizedLegalName.Should().Be("ARRI INDIA EQUIPMENT PVT LTD");
        vendor.Status.Should().Be(VendorStatus.CANDIDATE);
        vendor.RiskRating.Should().Be(RiskRating.UNRATED);
    }

    [Fact]
    public void ApproveAndBlock_FollowsStateMachine()
    {
        var vendor = Vendor.Create(Guid.NewGuid(), "VND-LIGHT", "Light Works", VendorType.PARTNERSHIP, Guid.NewGuid());
        var user = Guid.NewGuid();

        vendor.Approve(user);
        vendor.Status.Should().Be(VendorStatus.ACTIVE);

        vendor.Block("Duplicate GSTIN reported during audit review", user);
        vendor.Status.Should().Be(VendorStatus.BLOCKED);
        vendor.BlockedReason.Should().Be("Duplicate GSTIN reported during audit review");

        vendor.Reactivate(user);
        vendor.Status.Should().Be(VendorStatus.ACTIVE);
        vendor.BlockedReason.Should().BeNull();
    }

    [Fact]
    public void MergeInto_Self_ThrowsArgumentException()
    {
        var vendor = Vendor.Create(Guid.NewGuid(), "VND-SELF", "Self Vendor", VendorType.INDIVIDUAL, Guid.NewGuid());
        
        var act = () => vendor.MergeInto(vendor.VendorId, Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Cannot merge vendor into itself*");
    }

    [Fact]
    public void ManualCommitment_FollowsStateMachine()
    {
        var orgId = Guid.NewGuid();
        var showId = Guid.NewGuid();
        var vendorId = Guid.NewGuid();
        var user = Guid.NewGuid();

        var commitment = ManualCommitment.Create(
            orgId: orgId,
            commitmentNumber: "CM-2026-001",
            showId: showId,
            vendorId: vendorId,
            departmentId: Guid.NewGuid(),
            categoryId: Guid.NewGuid(),
            commitmentType: CommitmentType.RENTAL,
            description: "Camera package rental for Block 1",
            amount: Money.From(450000.0000m, "INR"),
            validFrom: new DateOnly(2026, 12, 1),
            createdBy: user);

        commitment.Status.Should().Be(CommitmentStatus.DRAFT);
        commitment.Amount.Amount.Should().Be(450000.0000m);

        commitment.SubmitForApproval(user);
        commitment.Status.Should().Be(CommitmentStatus.PENDING_APPROVAL);

        commitment.Approve(user);
        commitment.Status.Should().Be(CommitmentStatus.OPEN);

        commitment.Close("Shoot completed and invoiced", user);
        commitment.Status.Should().Be(CommitmentStatus.CLOSED);
        commitment.CloseReason.Should().Be("Shoot completed and invoiced");
    }
}
