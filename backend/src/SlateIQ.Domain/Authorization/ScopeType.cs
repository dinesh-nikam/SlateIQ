using System;

namespace SlateIQ.Domain.Authorization;

/// <summary>
/// Canonical 7-tier nested scope hierarchy (SLQ-ARCH-003 §5.4):
/// ORG > SHOW > SEASON > EPISODE > DEPARTMENT > ASSIGNMENT > SELF
/// </summary>
public enum ScopeType
{
    SELF = 1,
    ASSIGNMENT = 2,
    DEPARTMENT = 3,
    EPISODE = 4,
    SEASON = 5,
    SHOW = 6,
    ORG = 7
}

public static class ScopeHierarchyExtensions
{
    /// <summary>
    /// Returns true if parent scope contains or encompasses the child target scope.
    /// Example: ORG contains SHOW, SHOW contains DEPARTMENT, etc.
    /// </summary>
    public static bool Encompasses(this ScopeType parent, ScopeType child)
    {
        return (int)parent >= (int)child;
    }
}
