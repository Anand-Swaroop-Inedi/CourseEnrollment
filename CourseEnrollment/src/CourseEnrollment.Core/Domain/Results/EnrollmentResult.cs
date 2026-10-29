using CourseEnrollment.Core.Domain.Enums;

namespace CourseEnrollment.Core.Domain.Results;

public sealed record EnrollmentResult
{
    public bool Success { get; init; }
    public EnrollmentStatus Status { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? PromotedLearnerId { get; init; }
    public int EnrolledCount { get; init; }
    public int WaitlistCount { get; init; }
}