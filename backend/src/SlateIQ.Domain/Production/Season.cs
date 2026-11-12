using SlateIQ.Domain.Common.Primitives;

namespace SlateIQ.Domain.Production;

public enum SeasonStatus
{
    PLANNED,
    ACTIVE,
    COMPLETED
}

public sealed class Season : Entity<Guid>
{
    public Guid SeasonId => Id;
    public Guid OrgId { get; private set; }
    public Guid ShowId { get; private set; }
    public int SeasonNumber { get; private set; }
    public string? Title { get; private set; }
    public int? PlannedEpisodes { get; private set; }
    public DateOnly? PlannedStartDate { get; private set; }
    public DateOnly? PlannedEndDate { get; private set; }
    public SeasonStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public long RowVersion { get; private set; } = 1;

    private Season() { }

    public static Season Create(
        Guid orgId,
        Guid showId,
        int seasonNumber,
        Guid createdBy,
        string? title = null,
        int? plannedEpisodes = null,
        DateOnly? plannedStartDate = null,
        DateOnly? plannedEndDate = null)
    {
        if (seasonNumber < 1)
        {
            throw new ArgumentException("Season number must be positive (>= 1).", nameof(seasonNumber));
        }

        var now = DateTimeOffset.UtcNow;
        return new Season
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            ShowId = showId,
            SeasonNumber = seasonNumber,
            Title = title?.Trim(),
            PlannedEpisodes = plannedEpisodes,
            PlannedStartDate = plannedStartDate,
            PlannedEndDate = plannedEndDate,
            Status = SeasonStatus.PLANNED,
            CreatedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now,
            UpdatedBy = createdBy,
            RowVersion = 1
        };
    }

    public void Activate(Guid updatedBy)
    {
        Status = SeasonStatus.ACTIVE;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
        RowVersion++;
    }

    public void Complete(Guid updatedBy)
    {
        Status = SeasonStatus.COMPLETED;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
        RowVersion++;
    }
}
