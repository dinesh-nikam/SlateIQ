using System;

namespace SlateIQ.Domain.Authorization;

/// <summary>
/// Strongly typed canonical permission key `<domain>.<resource>.<action>` (SLQ-ARCH-003 Appendix A).
/// </summary>
public readonly struct PermissionKey : IEquatable<PermissionKey>
{
    public string Domain { get; }
    public string Resource { get; }
    public string Action { get; }
    public string Value { get; }

    public PermissionKey(string domain, string resource, string action)
    {
        if (string.IsNullOrWhiteSpace(domain)) throw new ArgumentException("Domain is required", nameof(domain));
        if (string.IsNullOrWhiteSpace(resource)) throw new ArgumentException("Resource is required", nameof(resource));
        if (string.IsNullOrWhiteSpace(action)) throw new ArgumentException("Action is required", nameof(action));

        Domain = domain.Trim().ToLowerInvariant();
        Resource = resource.Trim().ToLowerInvariant();
        Action = action.Trim().ToLowerInvariant();
        Value = $"{Domain}.{Resource}.{Action}";
    }

    public static PermissionKey Parse(string permissionString)
    {
        if (string.IsNullOrWhiteSpace(permissionString))
            throw new ArgumentException("Permission string cannot be empty", nameof(permissionString));

        var parts = permissionString.Split('.');
        if (parts.Length != 3)
            throw new ArgumentException($"Invalid permission key format: {permissionString}. Expected domain.resource.action", nameof(permissionString));

        return new PermissionKey(parts[0], parts[1], parts[2]);
    }

    public static readonly PermissionKey ExpenseSubmit = new("expense", "submission", "submit");
    public static readonly PermissionKey ExpenseApprove = new("expense", "approval", "approve");
    public static readonly PermissionKey ExpenseReject = new("expense", "approval", "reject");
    public static readonly PermissionKey BudgetCreate = new("budget", "version", "create");
    public static readonly PermissionKey BudgetApprove = new("budget", "version", "approve");
    public static readonly PermissionKey AuditRead = new("auditops", "report", "read");

    public bool Equals(PermissionKey other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is PermissionKey other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value;
}
