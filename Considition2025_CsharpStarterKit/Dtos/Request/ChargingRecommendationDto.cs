namespace Considition2025_CsharpStarterKit.Dtos.Request;

public record ChargingRecommendationDto
{
    public required string NodeId { get; set; }
    public required float ChargeTo { get; set; }
}
