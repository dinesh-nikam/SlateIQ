using SlateIQ.Domain.Common.Primitives;

namespace SlateIQ.Domain.AuditOps;

public enum AssignmentType
{
    INTERNAL,
    PLATFORM_APPOINTED,
    EXTERNAL
}

public enum PermissionLevel
{
    READ_ONLY,
    CAPTURE,
    REVIEW
}

public enum AssignmentStatus
{
    PLANNED,
    ACTIVE,
    COMPLETED,
    REVOKED
}

public enum FindingSeverity
{
    LOW,
    MEDIUM,
    HIGH,
    CRITICAL
}

public enum FindingStatus
{
    DRAFT,
    OPEN,
    RESOLVED,
    CLOSED,
    WITHDRAWN
}

public enum FindingVisibility
{
    APPOINTING_PARTY_ONLY,
    SHARED_WITH_PRODUCTION
}

/// <summary>
/// Root Aggregate for Auditor's Mandate on a Show in SlateIQ (MOD-06).
/// Governed by state machine SM-08.
/// </summary>
public sealed class AuditAssignment : Entity<Guid>
{
    public Guid AuditAssignmentId => Id;
    public Guid OrgId { get; private set; }
    public Guid ShowId { get; private set; }
    public Guid AuditorUserId { get; private set; }
    public Guid? TeamId { get; private set; }
    public AssignmentType AssignmentType { get; private set; }
    public Guid? AppointingPartyId { get; private set; }
    public Guid? GrantId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public PermissionLevel PermissionLevel { get; private set; }
    public AssignmentStatus Status { get; private set; }
    public DateTimeOffset? HandedOverAt { get; private set; }
    public Guid? RevokedBy { get; private set; }
    public string? RevokeReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public long RowVersion { get; private set; } = 1;

    private AuditAssignment() { }

    public static AuditAssignment Create(
        Guid orgId,
        Guid showId,
        Guid auditorUserId,
        AssignmentType assignmentType,
        DateOnly startDate,
        DateOnly endDate,
        PermissionLevel permissionLevel,
        Guid createdBy,
        Guid? teamId = null,
        Guid? appointingPartyId = null,
        Guid? grantId = null)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("End date cannot precede start date (BR-AUD-001).", nameof(endDate));
        }

        if (assignmentType == AssignmentType.EXTERNAL && !grantId.HasValue)
        {
            throw new ArgumentException("External audit assignment requires cross-tenant grant ID reference.", nameof(grantId));
        }

        var now = DateTimeOffset.UtcNow;
        return new AuditAssignment
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            ShowId = showId,
            AuditorUserId = auditorUserId,
            TeamId = teamId,
            AssignmentType = assignmentType,
            AppointingPartyId = appointingPartyId,
            GrantId = grantId,
            StartDate = startDate,
            EndDate = endDate,
            PermissionLevel = permissionLevel,
            Status = AssignmentStatus.PLANNED,
            CreatedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now,
            UpdatedBy = createdBy,
            RowVersion = 1
        };
    }

    /// <summary>
    /// SM-08: PLANNED -> ACTIVE
    /// </summary>
    public void Activate(Guid updatedBy)
    {
        if (Status != AssignmentStatus.PLANNED)
        {
            throw new InvalidOperationException($"Cannot activate audit assignment in state '{Status}'. Must be PLANNED.");
        }

        Status = AssignmentStatus.ACTIVE;
        RecordUpdate(updatedBy);
    }

    /// <summary>
    /// SM-08: ACTIVE -> COMPLETED
    /// </summary>
    public void Complete(Guid updatedBy)
    {
        if (Status != AssignmentStatus.ACTIVE)
        {
            throw new InvalidOperationException($"Cannot complete audit assignment in state '{Status}'. Must be ACTIVE.");
        }

        Status = AssignmentStatus.COMPLETED;
        HandedOverAt = DateTimeOffset.UtcNow;
        RecordUpdate(updatedBy);
    }

    /// <summary>
    /// SM-08: * -> REVOKED
    /// </summary>
    public void Revoke(string reason, Guid revokedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (Status == AssignmentStatus.COMPLETED)
        {
            throw new InvalidOperationException("Cannot revoke a completed audit assignment.");
        }

        Status = AssignmentStatus.REVOKED;
        RevokedBy = revokedBy;
        RevokeReason = reason.Trim();
        RecordUpdate(revokedBy);
    }

    private void RecordUpdate(Guid updatedBy)
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
        RowVersion++;
    }
}

/// <summary>
/// Audit Observation / Finding requiring review or response (MOD-06).
/// Governed by state machine SM-11.
/// </summary>
public sealed class AuditFinding : Entity<Guid>
{
    public Guid FindingId => Id;
    public Guid OrgId { get; private set; }
    public Guid AuditAssignmentId { get; private set; }
    public Guid ShowId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public FindingSeverity Severity { get; private set; }
    public string FindingCategory { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string? LinkedEntityType { get; private set; }
    public Guid? LinkedEntityId { get; private set; }
    public FindingVisibility Visibility { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public FindingStatus Status { get; private set; }
    public string? ClosureReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public long RowVersion { get; private set; } = 1;

    private AuditFinding() { }

    public static AuditFinding Create(
        Guid orgId,
        Guid auditAssignmentId,
        Guid showId,
        FindingSeverity severity,
        string category,
        string title,
        string description,
        Guid createdBy,
        FindingVisibility visibility = FindingVisibility.SHARED_WITH_PRODUCTION,
        Guid? departmentId = null,
        string? linkedEntityType = null,
        Guid? linkedEntityId = null,
        DateOnly? dueDate = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title, nameof(title));
        ArgumentException.ThrowIfNullOrWhiteSpace(description, nameof(description));

        var now = DateTimeOffset.UtcNow;
        return new AuditFinding
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            AuditAssignmentId = auditAssignmentId,
            ShowId = showId,
            DepartmentId = departmentId,
            Severity = severity,
            FindingCategory = category.Trim(),
            Title = title.Trim(),
            Description = description.Trim(),
            LinkedEntityType = linkedEntityType,
            LinkedEntityId = linkedEntityId,
            Visibility = visibility,
            DueDate = dueDate,
            Status = FindingStatus.DRAFT,
            CreatedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now,
            UpdatedBy = createdBy,
            RowVersion = 1
        };
    }

    /// <summary>
    /// SM-11: DRAFT -> OPEN
    /// </summary>
    public void Open(Guid updatedBy)
    {
        if (Status != FindingStatus.DRAFT)
        {
            throw new InvalidOperationException($"Cannot open finding in state '{Status}'. Must be DRAFT.");
        }

        Status = FindingStatus.OPEN;
        RecordUpdate(updatedBy);
    }

    /// <summary>
    /// SM-11: OPEN -> RESOLVED
    /// </summary>
    public void Resolve(Guid updatedBy)
    {
        if (Status != FindingStatus.OPEN)
        {
            throw new InvalidOperationException($"Cannot resolve finding in state '{Status}'. Must be OPEN.");
        }

        Status = FindingStatus.RESOLVED;
        RecordUpdate(updatedBy);
    }

    /// <summary>
    /// SM-11: RESOLVED -> CLOSED
    /// </summary>
    public void Close(string closureReason, Guid updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(closureReason, nameof(closureReason));

        if (Status != FindingStatus.RESOLVED && Status != FindingStatus.OPEN)
        {
            throw new InvalidOperationException($"Cannot close finding in state '{Status}'. Must be OPEN or RESOLVED.");
        }

        Status = FindingStatus.CLOSED;
        ClosureReason = closureReason.Trim();
        RecordUpdate(updatedBy);
    }

    private void RecordUpdate(Guid updatedBy)
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
        RowVersion++;
    }
}
