using CourseEnrollment.Core.Domain.Enums;
using CourseEnrollment.Core.Domain.Models;
using CourseEnrollment.Core.Services;
using CourseEnrollment.Core.Storage;
using Xunit;

namespace CourseEnrollment.Tests.Unit;

public sealed class EnrollmentTests
{
    private const string SeededCourseId = "AI-301";
    private static readonly string[] ValidPrerequisites = { "MATH201", "CS201" };

    private readonly ICourseOfferingRepository _repository;
    private readonly CourseEnrollmentService _service;

    public EnrollmentTests()
    {
        _repository = new CourseOfferingRepository();
        _service = new CourseEnrollmentService(_repository);
    }

    [Fact]
    public async Task Enroll_WhenSeatsAvailableAndPrerequisitesMet_EnrollsSuccessfully()
    {
        var learner = new Learner("L1", ValidPrerequisites);

        var result = await _service.EnrollAsync(SeededCourseId, learner);

        Assert.True(result.Success);
        Assert.Equal(EnrollmentStatus.Enrolled, result.Status);
        Assert.Equal(1, result.EnrolledCount);
        Assert.Equal(0, result.WaitlistCount);
    }

    [Fact]
    public async Task Enroll_WhenPrerequisitesMissing_RejectsWithFailedPrerequisite()
    {
        // Missing "CS201"
        var learner = new Learner("L_NoPrereq", new[] { "MATH201" });

        var result = await _service.EnrollAsync(SeededCourseId, learner);

        Assert.False(result.Success);
        Assert.Equal(EnrollmentStatus.FailedPrerequisite, result.Status);
        Assert.Equal(0, result.EnrolledCount);
    }

    [Fact]
    public async Task Enroll_WhenAlreadyEnrolled_ReturnsDuplicateRequestWithoutStateChange()
    {
        var learner = new Learner("L1", ValidPrerequisites);
        await _service.EnrollAsync(SeededCourseId, learner);

        // Duplicate call
        var duplicateResult = await _service.EnrollAsync(SeededCourseId, learner);

        Assert.False(duplicateResult.Success);
        Assert.Equal(EnrollmentStatus.DuplicateRequest, duplicateResult.Status);
        Assert.Equal(1, duplicateResult.EnrolledCount);
    }

    [Fact]
    public async Task Enroll_WhenCapacityFull_AddsEligibleLearnerToWaitlist()
    {
        var l1 = new Learner("L1", ValidPrerequisites);
        var l2 = new Learner("L2", ValidPrerequisites);
        var l3 = new Learner("L3", ValidPrerequisites);

        await _service.EnrollAsync(SeededCourseId, l1);
        await _service.EnrollAsync(SeededCourseId, l2);

        // Third learner exceeds capacity (2) and must be placed on waitlist
        var waitlistResult = await _service.EnrollAsync(SeededCourseId, l3);

        Assert.True(waitlistResult.Success);
        Assert.Equal(EnrollmentStatus.Waitlisted, waitlistResult.Status);
        Assert.Equal(2, waitlistResult.EnrolledCount);
        Assert.Equal(1, waitlistResult.WaitlistCount);
    }

    [Fact]
    public async Task Enroll_WhenAlreadyOnWaitlist_ReturnsDuplicateRequest()
    {
        var l1 = new Learner("L1", ValidPrerequisites);
        var l2 = new Learner("L2", ValidPrerequisites);
        var l3 = new Learner("L3", ValidPrerequisites);

        await _service.EnrollAsync(SeededCourseId, l1);
        await _service.EnrollAsync(SeededCourseId, l2);
        await _service.EnrollAsync(SeededCourseId, l3);

        // Duplicate waitlist attempt for L3
        var duplicateWaitlistResult = await _service.EnrollAsync(SeededCourseId, l3);

        Assert.False(duplicateWaitlistResult.Success);
        Assert.Equal(EnrollmentStatus.DuplicateRequest, duplicateWaitlistResult.Status);
        Assert.Equal(1, duplicateWaitlistResult.WaitlistCount);
    }
}