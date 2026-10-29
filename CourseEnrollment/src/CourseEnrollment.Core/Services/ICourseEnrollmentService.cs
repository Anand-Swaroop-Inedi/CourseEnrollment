using CourseEnrollment.Core.Domain.Models;
using CourseEnrollment.Core.Domain.Results;

namespace CourseEnrollment.Core.Services;

public interface ICourseEnrollmentService
{
    /// <summary>
    /// Enrolls an eligible learner into the specified course offering, or places them on the FIFO waitlist if the course is at full capacity.
    /// </summary>
    /// <param name="courseId">The unique identifier of the course offering.</param>
    /// <param name="learner">The learner attempting to enroll, including their completed prerequisite course identifiers.</param>
    /// <returns>An <see cref="EnrollmentResult"/> detailing whether the learner was enrolled, waitlisted, or rejected due to failed prerequisites or duplicate entries.</returns>
    Task<EnrollmentResult> EnrollAsync(string courseId, Learner learner);

    /// <summary>
    /// Drops an enrolled or waitlisted learner from the course offering, automatically promoting the earliest eligible waitlisted learner if an enrolled seat opens.
    /// </summary>
    /// <param name="courseId">The unique identifier of the course offering.</param>
    /// <param name="learnerId">The unique identifier of the learner to remove.</param>
    /// <returns>An <see cref="EnrollmentResult"/> indicating drop status and identifying any learner automatically promoted from the waitlist.</returns>
    Task<EnrollmentResult> DropAsync(string courseId, string learnerId);

    /// <summary>
    /// Retrieves the current operational state of a specific course offering, including capacity, enrolled learners, and waitlist order.
    /// </summary>
    /// <param name="courseId">The unique identifier of the course offering.</param>
    /// <returns>The <see cref="CourseOffering"/> if found; otherwise, <see langword="null"/>.</returns>
    Task<CourseOffering?> GetCourseStateAsync(string courseId);

    /// <summary>
    /// Retrieves all active course offerings along with their current enrollment counts, capacity boundaries, and prerequisite requirements.
    /// </summary>
    /// <returns>A read-only collection of all available <see cref="CourseOffering"/> instances.</returns>
    Task<IReadOnlyCollection<CourseOffering>> GetAllCoursesAsync();
}
