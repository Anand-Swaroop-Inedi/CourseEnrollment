namespace CourseEnrollment.Core.Domain.Models;

public sealed class WaitlistEntry
{
    public WaitlistEntry(Learner learner, DateTimeOffset joinedAtUtc)
    {
        Learner = learner ?? throw new ArgumentNullException(nameof(learner));
        JoinedAtUtc = joinedAtUtc;
    }

    public Learner Learner { get; }
    public DateTimeOffset JoinedAtUtc { get; }

    public bool IsEligible(IReadOnlySet<string> requiredPrerequisites)
    {
        if (requiredPrerequisites.Count == 0)
        {
            return true;
        }

        return Learner.CompletedPrerequisites.IsSupersetOf(requiredPrerequisites);
    }
}