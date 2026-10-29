namespace CourseEnrollment.Core.Domain.Enums;

public enum EnrollmentStatus
{
    Enrolled,
    Waitlisted,
    Promoted,
    Dropped,
    FailedPrerequisite,
    DuplicateRequest,
    NotEnrolled,
    CourseNotFound
}