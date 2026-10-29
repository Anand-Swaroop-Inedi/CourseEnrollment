using CourseEnrollment.Core.Domain.Enums;
using CourseEnrollment.Core.Domain.Models;
using CourseEnrollment.Core.Services;
using CourseEnrollment.Core.Storage;
using Xunit;

namespace CourseEnrollment.Tests.Unit;

public sealed class CourseDropAndPromotionTests
{
    private readonly ICourseOfferingRepository _repository;
    private readonly CourseEnrollmentService _service;

    public CourseDropAndPromotionTests()
    {
        _repository = new CourseOfferingRepository();
        _service = new CourseEnrollmentService(_repository);
    }

    [Fact]
    public async Task Drop_EnrolledLearner_PromotesFirstEligibleWaitlistedLearnerFIFO()
    {
        // AI-301 seeded in CourseOfferingRepository has capacity 2 and requires MATH201, CS201
        const string courseId = "AI-301";
        var prereqs = new[] { "MATH201", "CS201" };

        var l1 = new Learner("L1", prereqs);
        var l2 = new Learner("L2", prereqs);
        var w1 = new Learner("Wait1", prereqs);
        var w2 = new Learner("Wait2", prereqs);

        await _service.EnrollAsync(courseId, l1);
        await _service.EnrollAsync(courseId, l2);
        await _service.EnrollAsync(courseId, w1);
        await _service.EnrollAsync(courseId, w2);

        // Act: Drop L1 -> Wait1 must be promoted first according to strict FIFO order
        var dropResult = await _service.DropAsync(courseId, "L1");

        // Assert
        Assert.True(dropResult.Success);
        Assert.Equal(EnrollmentStatus.Dropped, dropResult.Status);
        Assert.Equal("Wait1", dropResult.PromotedLearnerId);
        Assert.Equal(2, dropResult.EnrolledCount);
        Assert.Equal(1, dropResult.WaitlistCount);

        var state = await _service.GetCourseStateAsync(courseId);
        Assert.NotNull(state);
        Assert.Contains("Wait1", state.EnrolledLearners);
        Assert.DoesNotContain("L1", state.EnrolledLearners);
    }

    [Fact]
    public async Task Drop_WhenWaitlistHeadIsIneligible_EvictsHeadAndPromotesNextEligible()
    {
        // Custom single-capacity course requiring ADV999 (Title parameter supplied)
        const string courseId = "MATH300";
        var customCourse = new CourseOffering(courseId, "Advanced Mathematics", 1, new[] { "ADV999" });
        await _repository.SaveCourseAsync(customCourse);

        var enrolled = new Learner("L1", new[] { "ADV999" });
        await _service.EnrollAsync(courseId, enrolled);

        // Directly queue an ineligible candidate at head and an eligible candidate behind them
        var ineligibleWaiter = new Learner("IneligibleWait", Array.Empty<string>());
        var eligibleWaiter = new Learner("EligibleWait", new[] { "ADV999" });

        customCourse.AddWaitlist(new WaitlistEntry(ineligibleWaiter, DateTimeOffset.UtcNow));
        customCourse.AddWaitlist(new WaitlistEntry(eligibleWaiter, DateTimeOffset.UtcNow));

        // Act: Dropping L1 triggers promotion: IneligibleWait is evicted, EligibleWait is promoted
        var dropResult = await _service.DropAsync(courseId, "L1");

        // Assert
        Assert.True(dropResult.Success);
        Assert.Equal("EligibleWait", dropResult.PromotedLearnerId);
        Assert.Equal(1, dropResult.EnrolledCount);
        Assert.Equal(0, dropResult.WaitlistCount);

        var state = await _service.GetCourseStateAsync(courseId);
        Assert.NotNull(state);
        Assert.Contains("EligibleWait", state.EnrolledLearners);
        Assert.False(state.IsWaitlisted("IneligibleWait"));
    }

    [Fact]
    public async Task Drop_LearnerNotEnrolledOrWaitlisted_ReturnsNotEnrolledWithoutCorruptingState()
    {
        const string courseId = "AI-301";
        var l1 = new Learner("L1", new[] { "MATH201", "CS201" });
        await _service.EnrollAsync(courseId, l1);

        // Act: Attempt to drop learner who never joined
        var dropResult = await _service.DropAsync(courseId, "NonExistentLearner");

        // Assert
        Assert.False(dropResult.Success);
        Assert.Equal(EnrollmentStatus.NotEnrolled, dropResult.Status);
        Assert.Equal(1, dropResult.EnrolledCount);
    }

    [Fact]
    public async Task Drop_RepeatedDrop_DoesNotOpenPhantomSeats()
    {
        const string courseId = "AI-301";
        var l1 = new Learner("L1", new[] { "MATH201", "CS201" });
        await _service.EnrollAsync(courseId, l1);

        // First drop should succeed
        var firstDrop = await _service.DropAsync(courseId, "L1");
        Assert.True(firstDrop.Success);
        Assert.Equal(0, firstDrop.EnrolledCount);

        // Repeated drop on the same learner must be rejected cleanly
        var secondDrop = await _service.DropAsync(courseId, "L1");
        Assert.False(secondDrop.Success);
        Assert.Equal(EnrollmentStatus.NotEnrolled, secondDrop.Status);
        Assert.Equal(0, secondDrop.EnrolledCount);
    }
}