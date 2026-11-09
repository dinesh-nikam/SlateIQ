using System;
using SlateIQ.Domain.Common.ValueObjects;

namespace SlateIQ.Domain.Authorization;

public static class SeparationOfDutiesValidator
{
    /// <summary>
    /// Validates SOD-01 (BR-EXP-005, TS-09): Submitter cannot approve their own expense.
    /// </summary>
    public static void AssertSubmitterIsNotApprover(Guid submitterUserId, Guid approverUserId)
    {
        if (submitterUserId == approverUserId)
        {
            throw new InvalidOperationException("SELF_APPROVAL_PROHIBITED: Submitter cannot approve their own expense submission (BR-EXP-005).");
        }
    }

    /// <summary>
    /// Validates SOD-03 (BR-EXP-008, TS-10): Approver financial threshold limit.
    /// </summary>
    public static void AssertWithinApprovalLimit(Money transactionAmount, Money? approverLimit)
    {
        if (approverLimit.HasValue && transactionAmount > approverLimit.Value)
        {
            throw new InvalidOperationException($"APPROVAL_LIMIT_EXCEEDED: Transaction amount {transactionAmount} exceeds approver limit of {approverLimit.Value} (BR-EXP-008).");
        }
    }

    /// <summary>
    /// Validates SOD-02 (BR-BUD-012): Budget author cannot be the sole approver.
    /// </summary>
    public static void AssertAuthorIsNotSoleBudgetApprover(Guid authorUserId, Guid approverUserId)
    {
        if (authorUserId == approverUserId)
        {
            throw new InvalidOperationException("SOLE_AUTHOR_APPROVAL: Budget author cannot act as the sole approver (BR-BUD-012).");
        }
    }
}
