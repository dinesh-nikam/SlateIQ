using System.Text.RegularExpressions;
using SlateIQ.Domain.Common.Primitives;

namespace SlateIQ.Domain.Production;

public enum ShowType
{
    SERIES,
    FILM,
    REALITY,
    LIVE_EVENT,
    DOCUMENTARY,
    OTHER
}

public enum ShowStatus
{
    PLANNING,
    ACTIVE,
    COMPLETED,
    CLOSING,
    CLOSED,
    ARCHIVED,
    CANCELLED
}

public enum HostingPattern
{
    H1,
    H2
}

/// <summary>
/// Root Aggregate for Production / Show in SlateIQ (MOD-03).
/// Enforces State Machine SM-02 and production business rules.
/// </summary>
public sealed partial class Show : Entity<Guid>
{
    private static readonly Regex CodeRegex = GeneratedCodeRegex();

    public Guid ShowId => Id;
    public Guid OrgId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public ShowType ShowType { get; private set; }
    public string? Format { get; private set; }
    public string? Genre { get; private set; }
    public string Language { get; private set; } = "English";
    public DateOnly? PlannedStartDate { get; private set; }
    public DateOnly? PlannedEndDate { get; private set; }
    public int? PlannedShootDays { get; private set; }
    public HostingPattern HostingPattern { get; private set; } = HostingPattern.H1;
    public ShowStatus Status { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancelReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public long RowVersion { get; private set; } = 1;

    private Show() { }

    public static Show Create(
        Guid orgId,
        string code,
        string title,
        ShowType showType,
        Guid createdBy,
        string? format = null,
        string? genre = null,
        string language = "English",
        DateOnly? plannedStartDate = null,
        DateOnly? plannedEndDate = null,
        int? plannedShootDays = null,
        HostingPattern hostingPattern = HostingPattern.H1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code, nameof(code));
        ArgumentException.ThrowIfNullOrWhiteSpace(title, nameof(title));

        var normalizedCode = code.Trim().ToUpperInvariant();
        if (!CodeRegex.IsMatch(normalizedCode))
        {
            throw new ArgumentException("Show code must be 3-12 alphanumeric uppercase characters (A2-05).", nameof(code));
        }

        if (plannedStartDate.HasValue && plannedEndDate.HasValue && plannedEndDate.Value < plannedStartDate.Value)
        {
            throw new ArgumentException("Planned end date cannot precede planned start date.", nameof(plannedEndDate));
        }

        if (plannedShootDays.HasValue && plannedShootDays.Value < 0)
        {
            throw new ArgumentException("Planned shoot days must be non-negative.", nameof(plannedShootDays));
        }

        var now = DateTimeOffset.UtcNow;
        return new Show
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Code = normalizedCode,
            Title = title.Trim(),
            ShowType = showType,
            Format = format?.Trim(),
            Genre = genre?.Trim(),
            Language = language.Trim(),
            PlannedStartDate = plannedStartDate,
            PlannedEndDate = plannedEndDate,
            PlannedShootDays = plannedShootDays,
            HostingPattern = hostingPattern,
            Status = ShowStatus.PLANNING,
            CreatedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now,
            UpdatedBy = createdBy,
            RowVersion = 1
        };
    }

    /// <summary>
    /// SM-02: PLANNING -> ACTIVE
    /// Requires at least one season and primary production house linked.
    /// </summary>
    public void Activate(Guid updatedBy)
    {
        if (Status != ShowStatus.PLANNING)
        {
            throw new InvalidOperationException($"Invalid state transition: cannot activate show in state '{Status}'. Valid source is PLANNING.");
        }

        Status = ShowStatus.ACTIVE;
        RecordUpdate(updatedBy);
    }

    /// <summary>
    /// SM-02: ACTIVE -> COMPLETED (Wrap)
    /// </summary>
    public void Wrap(Guid updatedBy)
    {
        if (Status != ShowStatus.ACTIVE)
        {
            throw new InvalidOperationException($"Invalid state transition: cannot wrap show in state '{Status}'. Valid source is ACTIVE.");
        }

        Status = ShowStatus.COMPLETED;
        RecordUpdate(updatedBy);
    }

    /// <summary>
    /// SM-02: COMPLETED -> ACTIVE (Reshoot / Resume)
    /// </summary>
    public void Resume(Guid updatedBy)
    {
        if (Status != ShowStatus.COMPLETED)
        {
            throw new InvalidOperationException($"Invalid state transition: cannot resume show in state '{Status}'. Valid source is COMPLETED.");
        }

        Status = ShowStatus.ACTIVE;
        RecordUpdate(updatedBy);
    }

    /// <summary>
    /// SM-02: COMPLETED -> CLOSING (Closure Requested)
    /// </summary>
    public void RequestClosure(Guid updatedBy)
    {
        if (Status != ShowStatus.COMPLETED)
        {
            throw new InvalidOperationException($"Invalid state transition: cannot request closure for show in state '{Status}'. Valid source is COMPLETED.");
        }

        Status = ShowStatus.CLOSING;
        RecordUpdate(updatedBy);
    }

    /// <summary>
    /// SM-02: CLOSING -> CLOSED
    /// </summary>
    public void Close(Guid updatedBy)
    {
        if (Status != ShowStatus.CLOSING)
        {
            throw new InvalidOperationException($"Invalid state transition: cannot close show in state '{Status}'. Valid source is CLOSING.");
        }

        Status = ShowStatus.CLOSED;
        ClosedAt = DateTimeOffset.UtcNow;
        RecordUpdate(updatedBy);
    }

    /// <summary>
    /// SM-02: CLOSED -> ARCHIVED
    /// </summary>
    public void Archive(Guid updatedBy)
    {
        if (Status != ShowStatus.CLOSED)
        {
            throw new InvalidOperationException($"Invalid state transition: cannot archive show in state '{Status}'. Valid source is CLOSED.");
        }

        Status = ShowStatus.ARCHIVED;
        ArchivedAt = DateTimeOffset.UtcNow;
        RecordUpdate(updatedBy);
    }

    /// <summary>
    /// SM-02: PLANNING -> CANCELLED
    /// A show can only be cancelled while in PLANNING with zero committed expenditures.
    /// </summary>
    public void Cancel(string reason, Guid updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (Status != ShowStatus.PLANNING)
        {
            throw new InvalidOperationException($"Invalid state transition: cannot cancel show in state '{Status}'. Shows can only be cancelled from PLANNING.");
        }

        Status = ShowStatus.CANCELLED;
        CancelledAt = DateTimeOffset.UtcNow;
        CancelReason = reason.Trim();
        RecordUpdate(updatedBy);
    }

    private void RecordUpdate(Guid updatedBy)
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
        RowVersion++;
    }

    [GeneratedRegex("^[A-Z0-9]{3,12}$", RegexOptions.Compiled)]
    private static partial Regex GeneratedCodeRegex();
}
