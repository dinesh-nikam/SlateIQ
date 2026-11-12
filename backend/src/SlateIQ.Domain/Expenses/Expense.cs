using SlateIQ.Domain.Common.Primitives;
using SlateIQ.Domain.Common.ValueObjects;

namespace SlateIQ.Domain.Expenses;

public enum ExpenseStatus
{
    DRAFT,
    SUBMITTED,
    IN_REVIEW,
    APPROVED,
    POSTED,
    DISPUTED,
    REJECTED,
    VOID
}

public enum DocumentType
{
    TAX_INVOICE,
    BILL,
    RECEIPT,
    PETTY_CASH_VOUCHER,
    CREDIT_NOTE,
    OTHER
}

public enum PaymentMode
{
    CASH,
    PETTY_CASH,
    BANK_TRANSFER,
    CARD,
    UPI,
    CHEQUE,
    OTHER
}

/// <summary>
/// Root Aggregate for Expense in SlateIQ (MOD-07).
/// Enforces State Machine SM-09, scale-4 money, tax components, and split conservation.
/// </summary>
public sealed class Expense : Entity<Guid>
{
    private readonly List<ExpenseLine> _lines = [];

    public Guid ExpenseId => Id;
    public Guid OrgId { get; private set; }
    public Guid RootExpenseId { get; private set; }
    public int RevisionNo { get; private set; } = 1;
    public Guid? SupersedesExpenseId { get; private set; }
    public string? ExpenseNumber { get; private set; }
    public Guid ShowId { get; private set; }
    public Guid? SeasonId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public Guid? VendorId { get; private set; }
    public DateOnly ExpenseDate { get; private set; }
    public DocumentType DocumentType { get; private set; }
    public string? DocumentNumber { get; private set; }
    public PaymentMode? PaymentMode { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string Source { get; private set; } = "WEB";
    public string Currency { get; private set; } = "INR";
    public Money TotalNetAmount { get; private set; } = Money.Zero("INR");
    public Money TotalTaxAmount { get; private set; } = Money.Zero("INR");
    public Money TotalGrossAmount { get; private set; } = Money.Zero("INR");
    public ExpenseStatus Status { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public string? CancelReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public long RowVersion { get; private set; } = 1;

    public IReadOnlyList<ExpenseLine> Lines => _lines.AsReadOnly();

    private Expense() { }

    public static Expense CreateDraft(
        Guid orgId,
        Guid showId,
        DateOnly expenseDate,
        DocumentType documentType,
        string description,
        string currency,
        Guid createdBy,
        Guid? seasonId = null,
        Guid? departmentId = null,
        Guid? vendorId = null,
        string? documentNumber = null,
        PaymentMode? paymentMode = null,
        string source = "WEB")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description, nameof(description));
        ArgumentException.ThrowIfNullOrWhiteSpace(currency, nameof(currency));

        var expenseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        return new Expense
        {
            Id = expenseId,
            OrgId = orgId,
            RootExpenseId = expenseId,
            RevisionNo = 1,
            ShowId = showId,
            SeasonId = seasonId,
            DepartmentId = departmentId,
            VendorId = vendorId,
            ExpenseDate = expenseDate,
            DocumentType = documentType,
            DocumentNumber = documentNumber?.Trim(),
            PaymentMode = paymentMode,
            Description = description.Trim(),
            Source = source,
            Currency = currency.ToUpperInvariant(),
            TotalNetAmount = Money.Zero(currency),
            TotalTaxAmount = Money.Zero(currency),
            TotalGrossAmount = Money.Zero(currency),
            Status = ExpenseStatus.DRAFT,
            CreatedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now,
            UpdatedBy = createdBy,
            RowVersion = 1
        };
    }

    /// <summary>
    /// Adds costed line item and updates totals (INV-001, INV-002).
    /// </summary>
    public void AddLine(ExpenseLine line)
    {
        if (Status != ExpenseStatus.DRAFT)
        {
            throw new InvalidOperationException($"Cannot add lines to expense in state '{Status}'. Must be DRAFT.");
        }

        _lines.Add(line);
        RecalculateTotals();
    }

    /// <summary>
    /// SM-09: DRAFT -> SUBMITTED
    /// </summary>
    public void Submit(string expenseNumber, Guid submittedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expenseNumber, nameof(expenseNumber));

        if (Status != ExpenseStatus.DRAFT)
        {
            throw new InvalidOperationException($"Cannot submit expense in state '{Status}'. Must be DRAFT.");
        }

        if (_lines.Count == 0)
        {
            throw new InvalidOperationException("Cannot submit expense without line items.");
        }

        ExpenseNumber = expenseNumber.Trim();
        Status = ExpenseStatus.SUBMITTED;
        SubmittedAt = DateTimeOffset.UtcNow;
        RecordUpdate(submittedBy);
    }

    /// <summary>
    /// SM-09: SUBMITTED -> APPROVED
    /// </summary>
    public void Approve(Guid approvedBy)
    {
        if (Status != ExpenseStatus.SUBMITTED && Status != ExpenseStatus.IN_REVIEW)
        {
            throw new InvalidOperationException($"Cannot approve expense in state '{Status}'. Must be SUBMITTED or IN_REVIEW.");
        }

        Status = ExpenseStatus.APPROVED;
        ApprovedAt = DateTimeOffset.UtcNow;
        RecordUpdate(approvedBy);
    }

    /// <summary>
    /// SM-09: APPROVED -> POSTED
    /// </summary>
    public void Post(Guid postedBy)
    {
        if (Status != ExpenseStatus.APPROVED)
        {
            throw new InvalidOperationException($"Cannot post expense in state '{Status}'. Must be APPROVED.");
        }

        Status = ExpenseStatus.POSTED;
        RecordUpdate(postedBy);
    }

    /// <summary>
    /// SM-09: * -> VOID
    /// </summary>
    public void Void(string cancelReason, Guid voidedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cancelReason, nameof(cancelReason));

        if (Status == ExpenseStatus.POSTED)
        {
            throw new InvalidOperationException("Cannot void a POSTED expense. Must issue a credit note or reversal posting.");
        }

        Status = ExpenseStatus.VOID;
        CancelReason = cancelReason.Trim();
        RecordUpdate(voidedBy);
    }

    private void RecalculateTotals()
    {
        var net = Money.Zero(Currency);
        var tax = Money.Zero(Currency);

        foreach (var l in _lines)
        {
            net += l.NetAmount;
            tax += l.TaxAmount;
        }

        TotalNetAmount = net;
        TotalTaxAmount = tax;
        TotalGrossAmount = net + tax;
    }

    private void RecordUpdate(Guid updatedBy)
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
        RowVersion++;
    }
}

public sealed class ExpenseLine : Entity<Guid>
{
    public Guid ExpenseLineId => Id;
    public Guid OrgId { get; private set; }
    public Guid ExpenseId { get; private set; }
    public Guid ShowId { get; private set; }
    public short LineNo { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid DepartmentId { get; private set; }
    public string CostType { get; private set; }
    public Guid? EpisodeId { get; private set; }
    public string? Description { get; private set; }
    public decimal Quantity { get; private set; } = 1m;
    public Money UnitPrice { get; private set; }
    public decimal TaxRate { get; private set; } = 0m;
    public Money NetAmount { get; private set; }
    public Money TaxAmount { get; private set; }
    public Money GrossAmount { get; private set; }
    public Money CostBasisAmount { get; private set; }
    public Guid? CommitmentId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }

    private ExpenseLine() { }

    public static ExpenseLine Create(
        Guid orgId,
        Guid expenseId,
        Guid showId,
        short lineNo,
        Guid categoryId,
        Guid departmentId,
        string costType,
        decimal quantity,
        Money unitPrice,
        decimal taxRate,
        Guid createdBy,
        string? description = null,
        Guid? episodeId = null,
        Guid? commitmentId = null)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));
        }

        var net = Money.From(Math.Round(quantity * unitPrice.Amount, 4, MidpointRounding.AwayFromZero), unitPrice.Currency);
        var tax = Money.From(Math.Round(net.Amount * (taxRate / 100m), 4, MidpointRounding.AwayFromZero), unitPrice.Currency);
        var gross = net + tax;

        return new ExpenseLine
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            ExpenseId = expenseId,
            ShowId = showId,
            LineNo = lineNo,
            CategoryId = categoryId,
            DepartmentId = departmentId,
            CostType = costType,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TaxRate = taxRate,
            NetAmount = net,
            TaxAmount = tax,
            GrossAmount = gross,
            CostBasisAmount = gross, // In standard case full gross is cost basis
            Description = description?.Trim(),
            EpisodeId = episodeId,
            CommitmentId = commitmentId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy
        };
    }
}
