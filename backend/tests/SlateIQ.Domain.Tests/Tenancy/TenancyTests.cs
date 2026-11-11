using System;
using FluentAssertions;
using SlateIQ.Domain.Tenancy;
using Xunit;

namespace SlateIQ.Domain.Tests.Tenancy;

public class TenancyTests
{
    [Fact]
    public void CreateProvisioning_WithValidParameters_InitializesInProvisioningState()
    {
        var org = Organization.CreateProvisioning(
            code: "monsoon-media",
            legalName: "Monsoon Media Works Pvt Ltd",
            orgType: OrganizationType.PRODUCTION_HOUSE,
            baseCurrency: "INR",
            tier: OrganizationTier.STANDARD,
            homeRegion: "IN");

        org.Id.Should().NotBeEmpty();
        org.Code.Should().Be("monsoon-media");
        org.LegalName.Should().Be("Monsoon Media Works Pvt Ltd");
        org.Status.Should().Be(TenantStatus.PROVISIONING);
        org.BaseCurrency.Should().Be("INR");
        org.HomeRegion.Should().Be("IN");
        org.Version.Should().Be(1);
    }

    [Theory]
    [InlineData("ab")] // Too short
    [InlineData("a".PadRight(51, 'x'))] // Too long
    [InlineData("   ")] // Empty
    public void CreateProvisioning_WithInvalidCode_ThrowsArgumentException(string invalidCode)
    {
        var act = () => Organization.CreateProvisioning(
            code: invalidCode,
            legalName: "Invalid Corp",
            orgType: OrganizationType.STUDIO);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateProvisioning_WithInvalidRegion_ThrowsArgumentException()
    {
        var act = () => Organization.CreateProvisioning(
            code: "global-media",
            legalName: "Global Media",
            orgType: OrganizationType.OTT_PLATFORM,
            homeRegion: "US"); // Only IN and EU permitted per residency rules

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Activate_FromProvisioningState_TransitionsToActiveAndIncrementsVersion()
    {
        var org = Organization.CreateProvisioning("lotus-films", "Lotus Films", OrganizationType.STUDIO);
        
        org.Activate();

        org.Status.Should().Be(TenantStatus.ACTIVE);
        org.Version.Should().Be(2);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ThrowsInvalidOperationException()
    {
        var org = Organization.CreateProvisioning("lotus-films", "Lotus Films", OrganizationType.STUDIO);
        org.Activate();

        var act = () => org.Activate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot activate tenant in state 'ACTIVE'*");
    }

    [Fact]
    public void SuspendAndReactivate_TransitionsStateCorrectly()
    {
        var org = Organization.CreateProvisioning("lotus-films", "Lotus Films", OrganizationType.STUDIO);
        org.Activate();

        org.Suspend("COMMERCIAL_OVERDUE");
        org.Status.Should().Be(TenantStatus.SUSPENDED);
        org.Version.Should().Be(3);

        org.Reactivate();
        org.Status.Should().Be(TenantStatus.ACTIVE);
        org.Version.Should().Be(4);
    }

    [Fact]
    public void ProvisioningRun_Tracks10StepsAndCompletes()
    {
        var run = ProvisioningRun.Start("lotus-films", "IN", "idemp-001");

        run.Status.Should().Be(ProvisioningStatus.RUNNING);
        run.CurrentStep.Should().Be(1);
        run.StepLog.Should().BeEmpty();

        run.RecordStepSuccess(1, "ValidateRequest", "Passed");
        run.CurrentStep.Should().Be(2);
        run.StepLog.Should().HaveCount(1);

        run.RecordStepSuccess(2, "CreateOrganization", "Passed");
        run.CurrentStep.Should().Be(3);

        var orgId = Guid.NewGuid();
        run.Complete(orgId);

        run.Status.Should().Be(ProvisioningStatus.SUCCEEDED);
        run.OrgId.Should().Be(orgId);
        run.CompletedAt.Should().NotBeNull();
        run.CurrentStep.Should().Be(10);
    }

    [Fact]
    public void ProvisioningRun_RecordStepFailure_SetsStatusToFailed()
    {
        var run = ProvisioningRun.Start("lotus-films", "IN", "idemp-002");
        run.RecordStepSuccess(1, "ValidateRequest", "Passed");
        
        run.RecordStepFailure(2, "CreateOrganization", "DB connection timed out");

        run.Status.Should().Be(ProvisioningStatus.FAILED);
        run.LastError.Should().Be("DB connection timed out");
        run.StepLog.Should().HaveCount(2);
        run.StepLog[1].Succeeded.Should().BeFalse();
    }
}
