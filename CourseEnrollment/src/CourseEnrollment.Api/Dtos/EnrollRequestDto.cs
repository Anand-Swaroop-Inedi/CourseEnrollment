namespace CourseEnrollment.Api.Dtos;

public sealed record EnrollRequestDto
{
    public string LearnerId { get; init; } = string.Empty;
    public List<string> CompletedPrerequisites { get; init; } = new();
}