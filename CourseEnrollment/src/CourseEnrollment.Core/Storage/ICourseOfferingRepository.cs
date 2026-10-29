using CourseEnrollment.Core.Domain.Models;

namespace CourseEnrollment.Core.Storage;

public interface ICourseOfferingRepository
{
    /// <summary>
    /// Retrieves all course offerings currently maintained in storage.
    /// </summary>
    /// <returns>A read-only collection of all <see cref="CourseOffering"/> instances.</returns>
    Task<IReadOnlyCollection<CourseOffering>> GetAllCoursesAsync();

    /// <summary>
    /// Retrieves a specific course offering by its unique identifier.
    /// </summary>
    /// <param name="courseId">The unique identifier of the course offering to look up.</param>
    /// <returns>The <see cref="CourseOffering"/> if found; otherwise, <see langword="null"/>.</returns>
    Task<CourseOffering?> GetCourseAsync(string courseId);

    /// <summary>
    /// Persists or updates the specified course offering in storage.
    /// </summary>
    /// <param name="course">The <see cref="CourseOffering"/> aggregate root instance to persist.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="course"/> is <see langword="null"/>.</exception>
    Task SaveCourseAsync(CourseOffering course);
}