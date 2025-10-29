namespace Considition2025_CsharpStarterKit.Dtos.Request;

public record ChargingRecommendationDto
{
    public required string NodeId { get; set; }
    public float ChargeTo { get; set; } = 1.0f;
}