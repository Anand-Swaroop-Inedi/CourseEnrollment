namespace CourseEnrollment.Core.Domain.Models;

public sealed class Learner
{
    public Learner(string learnerId, IEnumerable<string>? completedPrerequisites = null)
    {
        if (string.IsNullOrWhiteSpace(learnerId))
        {
            throw new ArgumentException("LearnerId cannot be null or empty.", nameof(learnerId));
        }

        LearnerId = learnerId;
        CompletedPrerequisites = new HashSet<string>(completedPrerequisites ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
    }

    public string LearnerId { get; }
    public IReadOnlySet<string> CompletedPrerequisites { get; }
}