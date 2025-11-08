using System.Diagnostics;
using Considition2025_CsharpStarterKit;
using Considition2025_CsharpStarterKit.Dtos.Request;
using Considition2025_CsharpStarterKit.Dtos.Response;

var apiKey = "INSERT API KEY HERE";
var client = new ConsiditionClient("http://localhost:8080", apiKey);

const string mapName = "Turbohill";
var map = await client.GetMap(mapName);

if (map is null)
{
    Console.WriteLine("Failed to fetch map!");
    return;
}

var finalScore = 0.0f;
var goodTicks = new List<TickDto>();

// Initial input for the first tick
var currentTick = GenerateTick(map, 0);

var input = new GameInputDto
{
    MapName = mapName,
    Ticks = [currentTick]
};

for (var i = 0; i < map.Ticks; i++)
{
    while (true)
    {
        Console.WriteLine($"Playing tick: {i} with input: {input}");
        var gameResponse = await client.PostGame(input);

        if (gameResponse is null)
        {
            Console.WriteLine("Got no game response");
            return;
        }

        finalScore = gameResponse.Score;

        // Check if we are happy with the response
        if (ShouldMoveOnToNextTick(gameResponse))
        {
            // If we are, we save the current ticks in the list of good ticks
            goodTicks.Add(currentTick);

            // Generate new tick for next iteration
            currentTick = GenerateTick(gameResponse.Map, i + 1);

            // Set new input
            input = new GameInputDto
            {
                MapName = mapName,
                PlayToTick = i + 1,
                Ticks = [..goodTicks, currentTick]
            };
            break;
        }

        // Not happy with the result
        // Try with different input
        currentTick = GenerateTick(gameResponse.Map, i);

        input = new GameInputDto
        {
            MapName = mapName,
            PlayToTick = i,
            Ticks = [..goodTicks, currentTick]
        };
    }
}

Console.WriteLine($"Final score: {finalScore}");

return;

bool ShouldMoveOnToNextTick(GameResponseDto _response)
{
    // Implement logic to decide if the current tick should continue to be iterated on or move on to the next tick
    return true;
}

TickDto GenerateTick(MapDto _map, int _currentTick)
{
    // Implement logic to generate ticks for the optimal score
    return new TickDto
    {
        Tick = _currentTick,
        CustomerRecommendations = GenerateCustomerRecommendations(_map, _currentTick)
    };
}

List<CustomerRecommendationDto> GenerateCustomerRecommendations(MapDto _map, int _currentTick)
{
    // Implement logic to generate customer recommendation
    return [];
}