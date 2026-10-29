using CourseEnrollment.Api.Dtos;
using CourseEnrollment.Core.Domain.Enums;
using CourseEnrollment.Core.Domain.Models;
using CourseEnrollment.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace CourseEnrollment.Api.Controllers;

[ApiController]
[Route("api/courses")]
[Produces("application/json")]
public sealed class CoursesController : ControllerBase
{
    private readonly ICourseEnrollmentService _courseEnrollmentService;

    public CoursesController(ICourseEnrollmentService courseEnrollmentService)
    {
        _courseEnrollmentService = courseEnrollmentService;
    }

    /// <summary>
    /// Enrolls an eligible learner into the course, or queues them in FIFO waitlist if at full capacity.
    /// </summary>
    [HttpPost("{courseId}/enroll")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enroll(string courseId, [FromBody] EnrollRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request?.LearnerId))
        {
            return BadRequest(new { message = "LearnerId is required." });
        }

        var learner = new Learner(request.LearnerId, request.CompletedPrerequisites);
        var result = await _courseEnrollmentService.EnrollAsync(courseId, learner);

        return MapResultToActionResult(result);
    }

    /// <summary>
    /// Drops an enrolled or waitlisted learner. If an enrolled seat frees up, promotes the earliest eligible waitlisted candidate.
    /// </summary>
    [HttpDelete("{courseId}/learners/{learnerId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Drop(string courseId, string learnerId)
    {
        if (string.IsNullOrWhiteSpace(learnerId))
        {
            return BadRequest(new { message = "LearnerId is required." });
        }

        var result = await _courseEnrollmentService.DropAsync(courseId, learnerId);

        return MapResultToActionResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllCourses()
    {
        var courses = await _courseEnrollmentService.GetAllCoursesAsync();

        var response = courses.Select(course => new
        {
            course.CourseId,
            course.Title,
            course.Capacity,
            course.EnrolledCount,
            course.WaitlistCount,
            AvailableSeats = Math.Max(0, course.Capacity - course.EnrolledCount),
            RequiredPrerequisites = course.RequiredPrerequisites
        });

        return Ok(response);
    }

    [HttpGet("{courseId}")]
    public async Task<IActionResult> GetCourseState(string courseId)
    {
        var course = await _courseEnrollmentService.GetCourseStateAsync(courseId);
        if (course is null)
        {
            return NotFound(new { message = $"Course '{courseId}' was not found." });
        }

        return Ok(new
        {
            course.CourseId,
            course.Title,
            course.Capacity,
            course.EnrolledCount,
            course.WaitlistCount,
            AvailableSeats = Math.Max(0, course.Capacity - course.EnrolledCount),
            EnrolledLearners = course.EnrolledLearners,
            Waitlist = course.Waitlist.Select((entry, index) => new
            {
                Position = index + 1,
                entry.Learner.LearnerId,
                entry.JoinedAtUtc
            })
        });
    }

    private IActionResult MapResultToActionResult(Core.Domain.Results.EnrollmentResult result)
    {
        return result.Status switch
        {
            EnrollmentStatus.Enrolled or
            EnrollmentStatus.Waitlisted or
            EnrollmentStatus.Dropped => Ok(result),

            EnrollmentStatus.CourseNotFound => NotFound(result),

            EnrollmentStatus.DuplicateRequest => Conflict(result),

            EnrollmentStatus.FailedPrerequisite or
            EnrollmentStatus.NotEnrolled or _ => BadRequest(result)
        };
    }
}