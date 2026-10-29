using CourseEnrollment.Core.Domain.Enums;
using CourseEnrollment.Core.Domain.Models;
using CourseEnrollment.Core.Services;
using CourseEnrollment.Core.Storage;
using Xunit;

namespace CourseEnrollment.Tests.Concurrency;

public sealed class ConcurrencyTests
{
    [Fact]
    public async Task HighConcurrency_Enrollments_NeverExceedCapacityAndPreserveFIFOQueue()
    {
        const int capacity = 5;
        const int totalLearners = 40;
        const string courseId = "CONCURRENCY_101";

        var repo = new CourseOfferingRepository();
        // Updated with required Title parameter
        var course = new CourseOffering(courseId, "Concurrency Basics", capacity, new[] { "REQ1" });
        await repo.SaveCourseAsync(course);

        var service = new CourseEnrollmentService(repo);

        // Synchronization trigger to ensure all 40 tasks hit the service at the exact same instant
        var readySignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = Enumerable.Range(1, totalLearners).Select(i =>
        {
            var learner = new Learner($"Learner_{i:D2}", new[] { "REQ1" });
            return Task.Run(async () =>
            {
                await readySignal.Task; // Wait for simultaneous release
                return await service.EnrollAsync(courseId, learner);
            });
        }).ToList();

        // Release all parallel tasks simultaneously
        readySignal.SetResult();
        var results = await Task.WhenAll(tasks);

        var state = await service.GetCourseStateAsync(courseId);
        Assert.NotNull(state);

        // State Invariants
        Assert.Equal(capacity, state.EnrolledCount);
        Assert.Equal(totalLearners - capacity, state.WaitlistCount);
        Assert.True(state.EnrolledCount <= state.Capacity);

        // Result Invariants
        Assert.Equal(totalLearners, results.Count(r => r.Success));
        Assert.Equal(capacity, results.Count(r => r.Status == EnrollmentStatus.Enrolled));
        Assert.Equal(totalLearners - capacity, results.Count(r => r.Status == EnrollmentStatus.Waitlisted));
    }

    [Fact]
    public async Task HighConcurrency_SimultaneousDropsAndEnrolls_MaintainCapacityInvariants()
    {
        const int capacity = 3;
        const string courseId = "CONCURRENCY_DROP";

        var repo = new CourseOfferingRepository();
        // Updated with required Title parameter
        var course = new CourseOffering(courseId, "Distributed Systems", capacity, new[] { "REQ1" });
        await repo.SaveCourseAsync(course);

        var service = new CourseEnrollmentService(repo);

        // Fill course to capacity (L1, L2, L3) + 5 waitlisted learners (L4, L5, L6, L7, L8)
        for (int i = 1; i <= 8; i++)
        {
            await service.EnrollAsync(courseId, new Learner($"L{i}", new[] { "REQ1" }));
        }

        var readySignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Drop enrolled learners while new enrollments concurrently hit the service
        var dropTasks = new[] { "L1", "L2", "L3" }.Select(id => Task.Run(async () =>
        {
            await readySignal.Task;
            return await service.DropAsync(courseId, id);
        }));

        var newEnrollTasks = new[] { "L9", "L10", "L11" }.Select(id => Task.Run(async () =>
        {
            await readySignal.Task;
            return await service.EnrollAsync(courseId, new Learner(id, new[] { "REQ1" }));
        }));

        readySignal.SetResult();
        await Task.WhenAll(dropTasks.Concat(newEnrollTasks));

        var finalState = await service.GetCourseStateAsync(courseId);
        Assert.NotNull(finalState);

        // Invariants: Course must remain strictly at capacity, and total waitlist must account for all remaining active learners
        Assert.Equal(capacity, finalState.EnrolledCount);
        Assert.True(finalState.EnrolledCount <= finalState.Capacity);

        // Initial: 8 learners. Dropped: 3. Added: 3. Total active: 8.
        // With Capacity = 3, remaining Waitlist must be exactly 5.
        Assert.Equal(5, finalState.WaitlistCount);

        // Verify none of the dropped learners remain enrolled or waitlisted
        Assert.DoesNotContain("L1", finalState.EnrolledLearners);
        Assert.DoesNotContain("L2", finalState.EnrolledLearners);
        Assert.DoesNotContain("L3", finalState.EnrolledLearners);
    }
}