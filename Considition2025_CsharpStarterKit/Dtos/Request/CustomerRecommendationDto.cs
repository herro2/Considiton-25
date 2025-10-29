namespace Considition2025_CsharpStarterKit.Dtos.Request;

public record CustomerRecommendationDto
{
    public string? CustomerId { get; set; }
    public List<ChargingRecommendationDto> ChargingRecommendations { get; set; } = [];
}

public record ChargingRecommendationDto
{
    public string NodeId { get; set; }
    public float ChargeTo { get; set; }
}