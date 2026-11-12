using System;
using FluentAssertions;
using SlateIQ.Domain.Common.ValueObjects;
using SlateIQ.Domain.Expenses;
using Xunit;

namespace SlateIQ.Domain.Tests.Expenses;

public class ExpenseTests
{
    [Fact]
    public void CreateDraft_InitializesWithZeroTotals()
    {
        var orgId = Guid.NewGuid();
        var showId = Guid.NewGuid();
        var user = Guid.NewGuid();

        var expense = Expense.CreateDraft(
            orgId: orgId,
            showId: showId,
            expenseDate: new DateOnly(2026, 11, 12),
            documentType: DocumentType.TAX_INVOICE,
            description: "Location generator fuel",
            currency: "INR",
            createdBy: user);

        expense.ExpenseId.Should().NotBeEmpty();
        expense.RootExpenseId.Should().Be(expense.ExpenseId);
        expense.RevisionNo.Should().Be(1);
        expense.Status.Should().Be(ExpenseStatus.DRAFT);
        expense.TotalNetAmount.Amount.Should().Be(0m);
        expense.TotalTaxAmount.Amount.Should().Be(0m);
        expense.TotalGrossAmount.Amount.Should().Be(0m);
    }

    [Fact]
    public void AddLine_RecalculatesNetTaxAndGrossWithAwayFromZeroRounding()
    {
        var orgId = Guid.NewGuid();
        var showId = Guid.NewGuid();
        var user = Guid.NewGuid();

        var expense = Expense.CreateDraft(orgId, showId, new DateOnly(2026, 11, 12), DocumentType.TAX_INVOICE, "Fuel", "INR", user);

        // Line 1: 500 units @ 95.50 = 47,750.00, 18% GST = 8,595.00, Gross = 56,345.00
        var line1 = ExpenseLine.Create(
            orgId, expense.ExpenseId, showId, 1, Guid.NewGuid(), Guid.NewGuid(),
            "BELOW_THE_LINE", 500m, Money.From(95.5000m, "INR"), 18.00m, user);

        // Line 2: 1 unit @ 1,200.00 = 1,200.00, 5% GST = 60.00, Gross = 1,260.00
        var line2 = ExpenseLine.Create(
            orgId, expense.ExpenseId, showId, 2, Guid.NewGuid(), Guid.NewGuid(),
            "BELOW_THE_LINE", 1m, Money.From(1200.0000m, "INR"), 5.00m, user);

        expense.AddLine(line1);
        expense.AddLine(line2);

        expense.TotalNetAmount.Amount.Should().Be(48950.0000m);
        expense.TotalTaxAmount.Amount.Should().Be(8655.0000m);
        expense.TotalGrossAmount.Amount.Should().Be(57605.0000m);
        expense.TotalGrossAmount.Amount.Should().Be(expense.TotalNetAmount.Amount + expense.TotalTaxAmount.Amount);
    }

    [Fact]
    public void Submit_Approve_And_Post_Lifecycle()
    {
        var orgId = Guid.NewGuid();
        var showId = Guid.NewGuid();
        var user = Guid.NewGuid();

        var expense = Expense.CreateDraft(orgId, showId, new DateOnly(2026, 11, 12), DocumentType.TAX_INVOICE, "Props purchase", "INR", user);
        var line = ExpenseLine.Create(
            orgId, expense.ExpenseId, showId, 1, Guid.NewGuid(), Guid.NewGuid(),
            "BELOW_THE_LINE", 1m, Money.From(5000.0000m, "INR"), 18.00m, user);
        expense.AddLine(line);

        expense.Submit("MNS-26-0001", user);
        expense.Status.Should().Be(ExpenseStatus.SUBMITTED);
        expense.ExpenseNumber.Should().Be("MNS-26-0001");
        expense.SubmittedAt.Should().NotBeNull();

        var approver = Guid.NewGuid();
        expense.Approve(approver);
        expense.Status.Should().Be(ExpenseStatus.APPROVED);
        expense.ApprovedAt.Should().NotBeNull();

        var postingUser = Guid.NewGuid();
        expense.Post(postingUser);
        expense.Status.Should().Be(ExpenseStatus.POSTED);
    }

    [Fact]
    public void Void_WhenPosted_ThrowsInvalidOperationException()
    {
        var orgId = Guid.NewGuid();
        var showId = Guid.NewGuid();
        var user = Guid.NewGuid();

        var expense = Expense.CreateDraft(orgId, showId, new DateOnly(2026, 11, 12), DocumentType.TAX_INVOICE, "Props", "INR", user);
        expense.AddLine(ExpenseLine.Create(orgId, expense.ExpenseId, showId, 1, Guid.NewGuid(), Guid.NewGuid(), "BELOW_THE_LINE", 1m, Money.From(100m, "INR"), 0m, user));
        expense.Submit("MNS-26-0002", user);
        expense.Approve(user);
        expense.Post(user);

        var act = () => expense.Void("Error in billing", user);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot void a POSTED expense*");
    }
}
