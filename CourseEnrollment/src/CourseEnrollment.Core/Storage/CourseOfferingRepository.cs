using System.Collections.Concurrent;
using CourseEnrollment.Core.Domain.Models;

namespace CourseEnrollment.Core.Storage;

public sealed class CourseOfferingRepository : ICourseOfferingRepository
{
    private readonly ConcurrentDictionary<string, CourseOffering> _courses = new(StringComparer.OrdinalIgnoreCase);

    public CourseOfferingRepository()
    {
        var defaultCourse = new CourseOffering(
        courseId: "AI-301",
        title: "Artificial Intelligence Foundations",
        capacity: 2,
        requiredPrerequisites: new[] { "MATH201", "CS201" }
    );

        _courses.TryAdd(defaultCourse.CourseId, defaultCourse);
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<CourseOffering>> GetAllCoursesAsync()
    {
        IReadOnlyCollection<CourseOffering> list = _courses.Values.ToList();
        return Task.FromResult(list);
    }

    /// <inheritdoc />
    public Task<CourseOffering?> GetCourseAsync(string courseId)
    {
        _courses.TryGetValue(courseId, out var course);
        return Task.FromResult(course);
    }

    /// <inheritdoc />
    public Task SaveCourseAsync(CourseOffering course)
    {
        _courses[course.CourseId] = course;
        return Task.CompletedTask;
    }
}
