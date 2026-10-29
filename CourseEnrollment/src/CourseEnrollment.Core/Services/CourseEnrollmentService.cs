using CourseEnrollment.Core.Domain.Enums;
using CourseEnrollment.Core.Domain.Models;
using CourseEnrollment.Core.Domain.Results;
using CourseEnrollment.Core.Storage;

namespace CourseEnrollment.Core.Services;

public sealed class CourseEnrollmentService : ICourseEnrollmentService
{
    private readonly ICourseOfferingRepository _courseRepository;

    public CourseEnrollmentService(ICourseOfferingRepository courseRepository)
    {
        _courseRepository = courseRepository;
    }

    /// <inheritdoc />
    public async Task<EnrollmentResult> EnrollAsync(string courseId, Learner learner)
    {
        if (string.IsNullOrWhiteSpace(courseId))
            return Failure(EnrollmentStatus.CourseNotFound, "Course ID is required.");

        var course = await _courseRepository.GetCourseAsync(courseId);
        if (course is null)
            return Failure(EnrollmentStatus.CourseNotFound, $"Course '{courseId}' was not found.");

        lock (course.SyncLock)
        {
            if (course.IsEnrolled(learner.LearnerId))
                return Result(false, EnrollmentStatus.DuplicateRequest, $"Learner '{learner.LearnerId}' is already enrolled.", enrolledCount: course.EnrolledCount, waitlistCount: course.WaitlistCount);

            if (course.IsWaitlisted(learner.LearnerId))
                return Result(false, EnrollmentStatus.DuplicateRequest, $"Learner '{learner.LearnerId}' is already waitlisted.", enrolledCount: course.EnrolledCount, waitlistCount: course.WaitlistCount);

            if (!course.IsEligible(learner))
                return Result(false, EnrollmentStatus.FailedPrerequisite, $"Learner '{learner.LearnerId}' does not satisfy prerequisites.", enrolledCount: course.EnrolledCount, waitlistCount: course.WaitlistCount);

            if (course.EnrolledCount < course.Capacity)
            {
                course.AddEnrolled(learner.LearnerId);
                return Result(true, EnrollmentStatus.Enrolled, $"Learner '{learner.LearnerId}' enrolled successfully.", enrolledCount: course.EnrolledCount, waitlistCount: course.WaitlistCount);
            }

            course.AddWaitlist(new WaitlistEntry(learner, DateTimeOffset.UtcNow));
            return Result(true, EnrollmentStatus.Waitlisted, $"Course is full. Learner '{learner.LearnerId}' placed on waitlist.", enrolledCount: course.EnrolledCount, waitlistCount: course.WaitlistCount);
        }
    }

    /// <inheritdoc />
    public async Task<EnrollmentResult> DropAsync(string courseId, string learnerId)
    {
        if (string.IsNullOrWhiteSpace(courseId))
            return Failure(EnrollmentStatus.CourseNotFound, "Course ID is required.");

        var course = await _courseRepository.GetCourseAsync(courseId);
        if (course is null)
            return Failure(EnrollmentStatus.CourseNotFound, $"Course '{courseId}' was not found.");

        lock (course.SyncLock)
        {
            if (!course.IsEnrolled(learnerId) && !course.IsWaitlisted(learnerId))
            {
                return Result(false, EnrollmentStatus.NotEnrolled, $"Learner '{learnerId}' is not enrolled or waitlisted.", enrolledCount: course.EnrolledCount, waitlistCount: course.WaitlistCount);
            }

            if (course.IsEnrolled(learnerId))
            {
                course.RemoveEnrolled(learnerId);
                var promotedLearnerId = PromoteEarliestEligible(course);

                return Result(true, EnrollmentStatus.Dropped, $"Learner '{learnerId}' dropped.", promotedLearnerId: promotedLearnerId, enrolledCount: course.EnrolledCount, waitlistCount: course.WaitlistCount);
            }

            course.RemoveWaitlist(learnerId);
            return Result(true, EnrollmentStatus.Dropped, $"Learner '{learnerId}' removed from waitlist.", enrolledCount: course.EnrolledCount, waitlistCount: course.WaitlistCount);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<CourseOffering>> GetAllCoursesAsync()
    {
        return await _courseRepository.GetAllCoursesAsync();
    }

    /// <inheritdoc />
    public async Task<CourseOffering?> GetCourseStateAsync(string courseId)
    {
        return await _courseRepository.GetCourseAsync(courseId);
    }

    private static string? PromoteEarliestEligible(CourseOffering course)
    {
        while (course.WaitlistCount > 0 && course.EnrolledCount < course.Capacity)
        {
            var candidate = course.DequeueWaitlist();
            if (candidate is null) break;

            if (course.IsEligible(candidate.Learner))
            {
                course.AddEnrolled(candidate.Learner.LearnerId);
                return candidate.Learner.LearnerId;
            }
            // Skip-and-Evict: Ineligible waitlisted candidate is evicted, loop proceeds to next candidate
        }

        return null;
    }

    private static EnrollmentResult Result(bool success, EnrollmentStatus status, string message, string? promotedLearnerId = null, int enrolledCount = 0, int waitlistCount = 0)
        => new()
        {
            Success = success,
            Status = status,
            Message = message,
            PromotedLearnerId = promotedLearnerId,
            EnrolledCount = enrolledCount,
            WaitlistCount = waitlistCount
        };

    private static EnrollmentResult Failure(EnrollmentStatus status, string message)
        => new()
        {
            Success = false,
            Status = status,
            Message = message
        };
}