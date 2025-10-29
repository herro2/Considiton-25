using System.Text.Json.Serialization;

namespace Considition2025_CsharpStarterKit.Dtos.Response;

public record CustomerDto
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public required string Persona { get; init; }
    public required string FromNode { get; init; }
    public required string ToNode { get; init; }
    public int DepartureTick { get; set; }
    public float ChargeRemaining { get; set; }
    public float MaxCharge { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CustomerState State { get; set; }
}