namespace CourseEnrollment.Core.Domain.Models;

public sealed class CourseOffering
{
    private readonly object _syncLock = new();
    private readonly HashSet<string> _enrolledLearners;
    private readonly List<WaitlistEntry> _waitlist;
    private readonly HashSet<string> _requiredPrerequisites;

    public CourseOffering(
        string courseId,
        string title,
        int capacity,
        IEnumerable<string>? requiredPrerequisites = null)
    {
        if (string.IsNullOrWhiteSpace(courseId))
            throw new ArgumentException("CourseId cannot be null or empty.", nameof(courseId));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be null or empty.", nameof(title));

        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");

        CourseId = courseId;
        Title = title;
        Capacity = capacity;
        _requiredPrerequisites = new HashSet<string>(requiredPrerequisites ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        _enrolledLearners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _waitlist = new List<WaitlistEntry>();
    }

    public string CourseId { get; }
    public string Title { get; }
    public int Capacity { get; }
    public object SyncLock => _syncLock;

    public IReadOnlySet<string> RequiredPrerequisites => _requiredPrerequisites;

    public IReadOnlyCollection<string> EnrolledLearners
    {
        get { lock (_syncLock) { return _enrolledLearners.ToList(); } }
    }

    public IReadOnlyList<WaitlistEntry> Waitlist
    {
        get { lock (_syncLock) { return _waitlist.ToList(); } }
    }

    public int EnrolledCount
    {
        get { lock (_syncLock) { return _enrolledLearners.Count; } }
    }

    public int WaitlistCount
    {
        get { lock (_syncLock) { return _waitlist.Count; } }
    }

    public bool IsEligible(Learner learner)
    {
        if (_requiredPrerequisites.Count == 0) return true;
        return learner.CompletedPrerequisites.IsSupersetOf(_requiredPrerequisites);
    }

    public bool IsEnrolled(string learnerId)
    {
        lock (_syncLock) return _enrolledLearners.Contains(learnerId);
    }

    public bool IsWaitlisted(string learnerId)
    {
        lock (_syncLock) return _waitlist.Any(w => string.Equals(w.Learner.LearnerId, learnerId, StringComparison.OrdinalIgnoreCase));
    }

    public void AddEnrolled(string learnerId)
    {
        lock (_syncLock) _enrolledLearners.Add(learnerId);
    }

    public bool RemoveEnrolled(string learnerId)
    {
        lock (_syncLock) return _enrolledLearners.Remove(learnerId);
    }

    public void AddWaitlist(WaitlistEntry entry)
    {
        lock (_syncLock) _waitlist.Add(entry);
    }

    public bool RemoveWaitlist(string learnerId)
    {
        lock (_syncLock)
        {
            var index = _waitlist.FindIndex(w => string.Equals(w.Learner.LearnerId, learnerId, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _waitlist.RemoveAt(index);
                return true;
            }
            return false;
        }
    }

    public WaitlistEntry? DequeueWaitlist()
    {
        lock (_syncLock)
        {
            if (_waitlist.Count == 0) return null;
            var entry = _waitlist[0];
            _waitlist.RemoveAt(0);
            return entry;
        }
    }
}