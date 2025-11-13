using System.Net.Http.Json;
using Considition2025_CsharpStarterKit.Dtos.Request;
using Considition2025_CsharpStarterKit.Dtos.Response;

namespace Considition2025_CsharpStarterKit;

public class ConsiditionClient
{
    private readonly HttpClient client;

    public ConsiditionClient(string _baseUri, string? _apiKey = null)
    {
        client = new HttpClient();
        client.BaseAddress = new Uri(_baseUri);
        if (!string.IsNullOrEmpty(_apiKey))
        {
            client.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        }
    }

    public async Task<GameResponseDto?> PostGame(GameInputDto _inputDto)
    {
        var response = await client.PostAsJsonAsync("game", _inputDto);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<GameResponseDto>();
    }

    public async Task<MapDto?> GetMap(string _mapName)
    {
        return await client.GetFromJsonAsync<MapDto>($"map?mapName={_mapName}");
    }
}