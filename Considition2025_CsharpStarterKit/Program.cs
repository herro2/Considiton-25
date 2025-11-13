/*
 * Considition 2025 - Advanced EV Charging Optimizer
 *
 * Key Improvements:
 * - Zone-aware graph with pricing and renewable energy information
 * - Multi-stop charging strategy for long journeys
 * - Advanced station scoring (availability, speed, congestion, zones, renewables)
 * - Enhanced simulation state with time-based queue prediction
 * - Charging duration estimation and overlap detection
 * - Cost-benefit analysis for charging decisions
 * - Renewable energy preference for sustainability
 * - Emergency charging fallbacks for critical situations
 */

using Considition2025_CsharpStarterKit;
using Considition2025_CsharpStarterKit.Dtos.Request;
using Considition2025_CsharpStarterKit.Dtos.Response;
using System.Text;

// Configuration
var apiKey = "29b3af80-a90c-4e58-b5c9-703e517d6a2a";
var useLocalDocker = false;
var baseUrl = useLocalDocker ? "http://localhost:8080" : "https://api.considition.com";
const string mapName = "Clutchfield";

var client = new ConsiditionClient(baseUrl, apiKey);
Console.WriteLine($"Fetching map: {mapName}");
var map = await client.GetMap(mapName);

if (map is null)
{
    Console.WriteLine("Failed to fetch map!");
    return;
}

RenderMap(map);
Console.WriteLine($"Map loaded: {map.Ticks} ticks");

var graph = new Graph(map);
var pathCache = new Dictionary<string, Path>();
var simState = new SimulationState(map);

if (useLocalDocker)
{
    await RunLocalMode(client, mapName, map, graph, pathCache, simState);
}
else
{
    await RunCloudMode(client, mapName, map, graph, pathCache, simState);
}

return;

async Task RunLocalMode(ConsiditionClient client, string mapName, MapDto map, Graph graph,
    Dictionary<string, Path> pathCache, SimulationState simState)
{
    Console.WriteLine("Running in LOCAL mode (incremental)");
    var finalScore = 0.0f;
    var goodTicks = new List<TickDto>();

    for (var i = 0; i < map.Ticks; i++)
    {
        var currentTick = GenerateTick(map, i, graph, pathCache, simState);
        goodTicks.Add(currentTick);

        var input = new GameInputDto
        {
            MapName = mapName,
            PlayToTick = (byte)i,
            Ticks = [.. goodTicks]
        };

        Console.WriteLine($"Playing tick: {i}");
        var gameResponse = await client.PostGame(input);

        if (gameResponse is null)
        {
            Console.WriteLine("Got no game response");
            return;
        }

        finalScore = gameResponse.Score;
        Console.WriteLine($"Score: {gameResponse.Score}, Customer: {gameResponse.CustomerCompletionScore}, Revenue: {gameResponse.KwhRevenue}");

        map = gameResponse.Map;
        simState.UpdateFromMap(map, i);
    }

    Console.WriteLine($"Final score: {finalScore}");
}

async Task RunCloudMode(ConsiditionClient client, string mapName, MapDto map, Graph graph,
    Dictionary<string, Path> pathCache, SimulationState simState)
{
    Console.WriteLine("Running in CLOUD mode (all ticks at once)");
    var allTicks = new List<TickDto>();

    Console.WriteLine("Generating all ticks...");
    for (var i = 0; i < map.Ticks; i++)
    {
        var tick = GenerateTick(map, i, graph, pathCache, simState);
        allTicks.Add(tick);

        if (i % 50 == 0)
        {
            Console.WriteLine($"Generated tick {i}/{map.Ticks}");
        }
    }

    Console.WriteLine($"Generated {allTicks.Count} ticks. Submitting to API...");

    var input = new GameInputDto
    {
        MapName = mapName,
        Ticks = allTicks,
        PlayToTick = null
    };

    var gameResponse = await client.PostGame(input);

    if (gameResponse is null)
    {
        Console.WriteLine("Got no game response from cloud API");
        return;
    }

    Console.WriteLine($"\n=== FINAL RESULTS ===");
    Console.WriteLine($"GameId: {gameResponse.GameId}");
    Console.WriteLine($"Total Score: {gameResponse.Score}");
    Console.WriteLine($"Customer Completion Score: {gameResponse.CustomerCompletionScore}");
    Console.WriteLine($"kWh Revenue: {gameResponse.KwhRevenue}");
}

void RenderMap(MapDto map)
{
    string[] rows = new string[map.DimY];
    for (int i = 0; i < rows.Length; i++)
    {
        rows[i] = new string(' ', map.DimX);
    }

    char NodeChar = '@';
    char CustomerChar = '*';

    for (int i = 0; i < map.Nodes.Count; i++)
    {
        NodeDto currentNode = map.Nodes[i];
        int x = (int)currentNode.PosX;
        int y = (int)currentNode.PosY;

        StringBuilder sb = new StringBuilder(rows[y]);
        if (currentNode.Target is ChargingStationDto charginStation)
        {
            int numberOfChargers = charginStation.AmountOfAvailableChargers;
            char num = numberOfChargers >= 10 ? '+' : (char)(numberOfChargers + 48);
            sb[x] = num;
        }
        else if (currentNode.Customers.Count == 0)
        {
            sb[x] = NodeChar;
        }
        else
        {
            sb[x] = CustomerChar;
        }
        rows[y] = sb.ToString();
    }

    ConsoleColor[] colors =
    {
        ConsoleColor.Red, ConsoleColor.Blue, ConsoleColor.Green, ConsoleColor.Magenta,
        ConsoleColor.DarkMagenta, ConsoleColor.DarkYellow, ConsoleColor.DarkGreen,
        ConsoleColor.DarkRed, ConsoleColor.Black
    };

    int[,] mapZones = new int[map.DimX, map.DimY];
    for (int i = 0; i < mapZones.GetLength(0); i++)
    {
        for (int j = 0; j < mapZones.GetLength(1); j++)
        {
            mapZones[i, j] = colors.Length - 1;
        }
    }

    for (int i = 0; i < map.Zones.Count; i++)
    {
        ZoneDto activeZone = map.Zones[i];
        for (int yCord = (int)activeZone.TopLeftY; yCord <= activeZone.BottomRightY; yCord++)
        {
            for (int xCord = (int)activeZone.TopLeftX; xCord <= activeZone.BottomRightX; xCord++)
            {
                mapZones[xCord, yCord] = i;
            }
        }
    }

    for (int i = 0; i < map.DimY; i++)
    {
        for (int j = 0; j < map.DimX; j++)
        {
            Console.BackgroundColor = colors[mapZones[j, i]];
            Console.Write(rows[i][j]);
            Console.BackgroundColor = ConsoleColor.Black;
        }
        Console.Write('\n');
    }

    Console.WriteLine($"{NodeChar} - Nod");
    Console.WriteLine($"{CustomerChar} - Finns en eller fler kunder");
    Console.WriteLine($"nummer - antal tillängliga laddstationer, + innebär över tio");
    for (int i = 0; i < map.Zones.Count; i++)
    {
        Console.ForegroundColor = colors[i];
        Console.WriteLine($"{map.Zones[i].Id} - {colors[i].ToString()}");
    }

    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine($"No zone - {colors.Last().ToString()}");
}

TickDto GenerateTick(MapDto _map, int _currentTick, Graph _graph,
    Dictionary<string, Path> _pathCache, SimulationState _simState)
{
    return new TickDto
    {
        Tick = (byte)_currentTick,
        CustomerRecommendations = GenerateCustomerRecommendations(_map, _currentTick, _graph, _pathCache, _simState)
    };
}

List<CustomerRecommendationDto> GenerateCustomerRecommendations(MapDto _map, int _currentTick,
    Graph _graph, Dictionary<string, Path> _pathCache, SimulationState _simState)
{
    var recommendations = new List<CustomerRecommendationDto>();
    var allCustomers = new List<CustomerDto>();

    foreach (var node in _map.Nodes)
    {
        allCustomers.AddRange(node.Customers);
    }
    foreach (var edge in _map.Edges)
    {
        allCustomers.AddRange(edge.Customers);
    }

    foreach (var customer in allCustomers)
    {
        var recommendation = PlanCustomerJourney(customer, _map, _currentTick, _graph, _pathCache, _simState);
        recommendations.Add(recommendation);
    }

    return recommendations;
}

CustomerRecommendationDto PlanCustomerJourney(CustomerDto customer, MapDto map, int currentTick,
    Graph graph, Dictionary<string, Path> pathCache, SimulationState simState)
{
    var cacheKey = $"{customer.FromNode}->{customer.ToNode}";

    if (!pathCache.ContainsKey(cacheKey))
    {
        var path = graph.FindShortestPath(customer.FromNode, customer.ToNode);
        if (path != null)
        {
            pathCache[cacheKey] = path;
        }
    }

    var cachedPath = pathCache.GetValueOrDefault(cacheKey);
    if (cachedPath == null)
    {
        return new CustomerRecommendationDto
        {
            CustomerId = customer.Id,
            ChargingRecommendations = []
        };
    }

    float energyPerKm = customer.EnergyConsumptionPerKm();
    float totalEnergyNeeded = cachedPath.TotalDistance * energyPerKm;

    // Check if we need any charging at all
    if (customer.ChargeRemaining >= totalEnergyNeeded * 1.15f)
    {
        return new CustomerRecommendationDto
        {
            CustomerId = customer.Id,
            ChargingRecommendations = []
        };
    }

    // Plan multi-stop charging strategy
    var chargingPlan = PlanMultiStopCharging(customer, cachedPath, graph, simState, currentTick, energyPerKm);

    return new CustomerRecommendationDto
    {
        CustomerId = customer.Id,
        ChargingRecommendations = chargingPlan
    };
}

List<ChargingRecommendationDto> PlanMultiStopCharging(CustomerDto customer, Path path, Graph graph,
    SimulationState simState, int currentTick, float energyPerKm)
{
    var recommendations = new List<ChargingRecommendationDto>();
    var battery = customer.ChargeRemaining;
    var distanceTraveled = 0f;

    // Get all charging stations on the path
    var stationsOnPath = new List<(int index, Node node, ChargingStationDto station)>();
    for (int i = 0; i < path.Nodes.Count; i++)
    {
        var node = graph.GetNode(path.Nodes[i]);
        if (node?.HasChargingStation == true && node.ChargingStation != null)
        {
            stationsOnPath.Add((i, node, node.ChargingStation));
        }
    }

    if (stationsOnPath.Count == 0)
    {
        return recommendations; // No stations available
    }

    // Simulate journey and plan charging stops
    for (int i = 0; i < path.Nodes.Count - 1; i++)
    {
        var edge = graph.GetEdge(path.Nodes[i], path.Nodes[i + 1]);
        if (edge == null) continue;

        var energyNeeded = edge.Length * energyPerKm;

        // Calculate remaining distance after this segment
        var remainingDistance = path.TotalDistance - distanceTraveled - edge.Length;
        var energyToEnd = remainingDistance * energyPerKm;

        // Check if we need to charge at this node
        var currentNode = graph.GetNode(path.Nodes[i]);
        if (currentNode?.HasChargingStation == true && ShouldChargeAtStation(
            battery, energyNeeded, energyToEnd, customer.MaxCharge, stationsOnPath, i, path))
        {
            // Calculate optimal charge amount
            var optimalCharge = CalculateOptimalCharge(
                customer, battery, energyToEnd, energyPerKm, currentNode, stationsOnPath, i, path, simState, currentTick);

            if (optimalCharge > battery + 0.5f) // Only charge if meaningful increase
            {
                float chargeToPercentage = Math.Clamp(optimalCharge / customer.MaxCharge, 0f, 1f);

                recommendations.Add(new ChargingRecommendationDto
                {
                    NodeId = currentNode.Id,
                    ChargeTo = chargeToPercentage
                });

                battery = optimalCharge;
                simState.RecordChargingSession(customer.Id, currentNode.Id, currentTick, chargeToPercentage);
            }
        }

        battery -= energyNeeded;
        distanceTraveled += edge.Length;

        // Emergency check: if we're going to run out before next station
        if (battery < energyNeeded * 0.5f && i < path.Nodes.Count - 2)
        {
            // Find nearest previous charging station and charge there
            var nearestStation = FindNearestChargingStation(path, i, graph);
            if (nearestStation != null && !recommendations.Any(r => r.NodeId == nearestStation.Id))
            {
                recommendations.Add(new ChargingRecommendationDto
                {
                    NodeId = nearestStation.Id,
                    ChargeTo = 0.9f
                });
                battery = customer.MaxCharge * 0.9f;
            }
        }
    }

    return recommendations;
}

bool ShouldChargeAtStation(float battery, float energyToNext, float energyToEnd,
    float maxCharge, List<(int index, Node node, ChargingStationDto station)> stationsOnPath,
    int currentIndex, Path path)
{
    // Always charge if battery is critically low
    if (battery < energyToNext * 1.2f)
        return true;

    // Don't charge if we have enough to reach destination comfortably
    if (battery >= energyToEnd * 1.3f)
        return false;

    // Check if there are better stations ahead
    var stationsAhead = stationsOnPath.Where(s => s.index > currentIndex).ToList();
    if (stationsAhead.Count == 0)
    {
        // This is the last station, charge if needed
        return battery < energyToEnd * 1.2f;
    }

    // Charge if we won't make it to the next station
    var nextStation = stationsAhead.First();
    // Note: distanceToNextStation calculation would require edge lookup from graph
    // For now, use simplified logic based on remaining energy
    return battery < energyToEnd * 0.8f;
}

float CalculateOptimalCharge(CustomerDto customer, float currentBattery, float energyToEnd,
    float energyPerKm, Node station, List<(int index, Node node, ChargingStationDto station)> stationsOnPath,
    int currentIndex, Path path, SimulationState simState, int currentTick)
{
    // Base target: enough to reach destination with margin
    float target = currentBattery + energyToEnd * 1.2f;

    // Factor in station quality
    var stationScore = ScoreStation(new StationInfo { Node = station, Station = station.ChargingStation! },
        path, simState, currentTick);

    // If this is a good station and not too crowded, charge more
    if (stationScore > 50f)
    {
        target = Math.Max(target, customer.MaxCharge * 0.8f);
    }

    // If there are more stations ahead, don't overcharge
    var stationsAhead = stationsOnPath.Where(s => s.index > currentIndex).ToList();
    if (stationsAhead.Count > 0)
    {
        target = Math.Min(target, customer.MaxCharge * 0.7f);
    }

    // Consider zone pricing if available
    if (station.Zone != null)
    {
        var renewableRatio = CalculateRenewableRatio(station.Zone);
        if (renewableRatio > 0.7f)
        {
            // Cheap renewable energy, charge more
            target = Math.Max(target, customer.MaxCharge * 0.85f);
        }
    }

    return Math.Clamp(target, currentBattery, customer.MaxCharge);
}

float CalculateRenewableRatio(ZoneDto zone)
{
    if (zone.EnergySources.Count == 0) return 0f;

    var renewableCapacity = zone.EnergySources
        .Where(s => s.Type == "wind" || s.Type == "solar" || s.Type == "hydro")
        .Sum(s => s.GenerationCapacity);

    var totalCapacity = zone.EnergySources.Sum(s => s.GenerationCapacity);

    return totalCapacity > 0 ? renewableCapacity / totalCapacity : 0f;
}

Node? FindNearestChargingStation(Path path, int currentIndex, Graph graph)
{
    // Look backwards for nearest charging station
    for (int i = currentIndex; i >= 0; i--)
    {
        var node = graph.GetNode(path.Nodes[i]);
        if (node?.HasChargingStation == true)
            return node;
    }
    return null;
}

float ScoreStation(StationInfo station, Path path, SimulationState simState, int currentTick)
{
    float score = 0f;

    // Charger availability (most important factor)
    int availableChargers = station.Station.AmountOfAvailableChargers;
    score += availableChargers * 15f;

    // Charge speed
    float chargeSpeed = station.Station.ChargeSpeedPerCharger;
    score += chargeSpeed * 8f;

    // Position in path (prefer earlier stations for flexibility)
    int index = path.Nodes.IndexOf(station.Node.Id);
    if (index >= 0)
    {
        float positionBonus = (float)(path.Nodes.Count - index) / path.Nodes.Count * 5f;
        score += positionBonus;
    }

    // Predicted congestion
    int predictedUsage = simState.GetStationUsage(station.Node.Id, currentTick);
    float congestionPenalty = Math.Min(predictedUsage, availableChargers) * 8f;
    score -= congestionPenalty;

    // If fully utilized, heavy penalty
    if (predictedUsage >= availableChargers)
    {
        score -= 50f;
    }

    // Zone-based factors
    if (station.Node.Zone != null)
    {
        var zone = station.Node.Zone;

        // Renewable energy bonus
        var renewableRatio = CalculateRenewableRatio(zone);
        score += renewableRatio * 10f;

        // Energy storage capacity (indicates reliability)
        var storageCapacity = zone.EnergyStorages.Sum(s => s.CapacityMWh);
        score += Math.Min(storageCapacity * 0.5f, 10f);

        // Avoid zones with insufficient generation
        var totalGeneration = zone.EnergySources.Sum(s => s.GenerationCapacity);
        if (totalGeneration < 10f)
        {
            score -= 15f;
        }
    }
    else
    {
        // Penalty for nodes outside zones (less reliable)
        score -= 5f;
    }

    // Broken chargers penalty (reliability concern)
    score -= station.Station.TotalAmountOfBrokenChargers * 2f;

    return score;
}

// Station and customer data structures
public class StationInfo
{
    public Node Node { get; set; } = null!;
    public ChargingStationDto Station { get; set; } = null!;
}

public class SimulationState
{
    private Dictionary<string, List<ChargingSessionInfo>> stationUsage = new();
    private Dictionary<string, CustomerStateInfo> customerStates = new();
    private Dictionary<string, List<float>> stationHistoricalLoad = new();
    private readonly MapDto initialMap;

    public SimulationState(MapDto map)
    {
        initialMap = map;

        foreach (var node in map.Nodes.Where(n => n.Target is ChargingStationDto))
        {
            stationUsage[node.Id] = new List<ChargingSessionInfo>();
            stationHistoricalLoad[node.Id] = new List<float>();
        }

        // Initialize customer states
        foreach (var node in map.Nodes)
        {
            foreach (var customer in node.Customers)
            {
                customerStates[customer.Id] = new CustomerStateInfo
                {
                    CustomerId = customer.Id,
                    LastKnownTick = 0,
                    LastKnownBattery = customer.ChargeRemaining,
                    State = customer.State,
                    CurrentNode = node.Id,
                    DestinationNode = customer.ToNode
                };
            }
        }

        foreach (var edge in map.Edges)
        {
            foreach (var customer in edge.Customers)
            {
                customerStates[customer.Id] = new CustomerStateInfo
                {
                    CustomerId = customer.Id,
                    LastKnownTick = 0,
                    LastKnownBattery = customer.ChargeRemaining,
                    State = customer.State,
                    DestinationNode = customer.ToNode
                };
            }
        }
    }

    public void RecordChargingSession(string customerId, string stationId, int tick, float chargeTo)
    {
        if (stationUsage.ContainsKey(stationId))
        {
            stationUsage[stationId].Add(new ChargingSessionInfo
            {
                CustomerId = customerId,
                StationId = stationId,
                Tick = tick,
                ChargeTo = chargeTo,
                EstimatedDuration = EstimateChargingDuration(customerId, chargeTo, stationId)
            });
        }
    }

    public int GetStationUsage(string stationId, int tick)
    {
        if (!stationUsage.ContainsKey(stationId))
            return 0;

        // Count overlapping charging sessions
        int count = 0;
        foreach (var session in stationUsage[stationId])
        {
            int sessionStart = session.Tick;
            int sessionEnd = session.Tick + session.EstimatedDuration;

            if (tick >= sessionStart && tick <= sessionEnd)
            {
                count++;
            }
        }

        return count;
    }

    public float GetStationLoad(string stationId, int tick)
    {
        var usage = GetStationUsage(stationId, tick);

        // Get station capacity
        var station = initialMap.Nodes.FirstOrDefault(n => n.Id == stationId)?.Target as ChargingStationDto;
        if (station == null) return 0f;

        return (float)usage / Math.Max(1, station.AmountOfAvailableChargers);
    }

    public void UpdateFromMap(MapDto map, int tick)
    {
        // Update customer states
        foreach (var node in map.Nodes)
        {
            foreach (var customer in node.Customers)
            {
                if (customerStates.ContainsKey(customer.Id))
                {
                    customerStates[customer.Id].LastKnownTick = tick;
                    customerStates[customer.Id].LastKnownBattery = customer.ChargeRemaining;
                    customerStates[customer.Id].State = customer.State;
                    customerStates[customer.Id].CurrentNode = node.Id;
                }
            }
        }

        // Update historical load for each station
        foreach (var stationId in stationUsage.Keys)
        {
            var load = GetStationLoad(stationId, tick);
            if (!stationHistoricalLoad.ContainsKey(stationId))
            {
                stationHistoricalLoad[stationId] = new List<float>();
            }
            stationHistoricalLoad[stationId].Add(load);
        }
    }

    private int EstimateChargingDuration(string customerId, float targetCharge, string stationId)
    {
        // Simple estimation: assume 5 ticks per 20% charge
        if (!customerStates.ContainsKey(customerId))
            return 5;

        var customerState = customerStates[customerId];
        float chargeNeeded = targetCharge - customerState.LastKnownBattery;

        // Get charging speed
        var station = initialMap.Nodes.FirstOrDefault(n => n.Id == stationId)?.Target as ChargingStationDto;
        if (station == null) return 5;

        // Rough estimation based on charge speed
        float baseDuration = chargeNeeded / Math.Max(0.1f, station.ChargeSpeedPerCharger);
        return Math.Max(1, (int)Math.Ceiling(baseDuration * 2)); // Convert to ticks
    }

    public CustomerStateInfo? GetCustomerState(string customerId)
    {
        return customerStates.GetValueOrDefault(customerId);
    }
}

public class ChargingSessionInfo
{
    public string CustomerId { get; set; } = string.Empty;
    public string StationId { get; set; } = string.Empty;
    public int Tick { get; set; }
    public float ChargeTo { get; set; }
    public int EstimatedDuration { get; set; }
}

public class CustomerStateInfo
{
    public string CustomerId { get; set; } = string.Empty;
    public int LastKnownTick { get; set; }
    public float LastKnownBattery { get; set; }
    public CustomerState State { get; set; }
    public string? CurrentNode { get; set; }
    public string? DestinationNode { get; set; }
}

public class Graph
{
    private readonly Dictionary<string, Node> nodes = new();
    private readonly Dictionary<string, List<Edge>> adjacencyList = new();
    private readonly Dictionary<string, ZoneDto> zones = new();
    private readonly MapDto map;

    public Graph(MapDto map)
    {
        this.map = map;

        // Build zone lookup
        foreach (var zone in map.Zones)
        {
            zones[zone.Id] = zone;
        }

        foreach (var nodeDto in map.Nodes)
        {
            var node = new Node
            {
                Id = nodeDto.Id,
                PosX = nodeDto.PosX,
                PosY = nodeDto.PosY,
                ZoneId = nodeDto.ZoneId,
                Zone = nodeDto.ZoneId != null ? zones.GetValueOrDefault(nodeDto.ZoneId) : null,
                HasChargingStation = nodeDto.Target is ChargingStationDto,
                ChargingStation = nodeDto.Target as ChargingStationDto
            };
            nodes[node.Id] = node;
            adjacencyList[node.Id] = new List<Edge>();
        }

        foreach (var edgeDto in map.Edges)
        {
            var edge = new Edge
            {
                From = edgeDto.FromNode,
                To = edgeDto.ToNode,
                Length = edgeDto.Length
            };
            adjacencyList[edge.From].Add(edge);

            var reverseEdge = new Edge
            {
                From = edgeDto.ToNode,
                To = edgeDto.FromNode,
                Length = edgeDto.Length
            };
            adjacencyList[edge.To].Add(reverseEdge);
        }

        Console.WriteLine($"Graph: {nodes.Count} nodes, {nodes.Count(n => n.Value.HasChargingStation)} charging stations, {zones.Count} zones");
    }

    public Node? GetNode(string nodeId) => nodes.GetValueOrDefault(nodeId);

    public Edge? GetEdge(string fromId, string toId)
    {
        if (!adjacencyList.ContainsKey(fromId)) return null;
        return adjacencyList[fromId].FirstOrDefault(e => e.To == toId);
    }

    public IEnumerable<Node> GetAllNodes() => nodes.Values;

    public IEnumerable<Node> GetChargingStations() => nodes.Values.Where(n => n.HasChargingStation);

    public Path? FindShortestPath(string fromNodeId, string toNodeId)
    {
        if (!nodes.ContainsKey(fromNodeId) || !nodes.ContainsKey(toNodeId))
            return null;

        if (fromNodeId == toNodeId)
        {
            return new Path
            {
                Nodes = new List<string> { fromNodeId },
                TotalDistance = 0
            };
        }

        var distances = new Dictionary<string, float>();
        var previous = new Dictionary<string, string>();
        var priorityQueue = new PriorityQueue<string, float>();
        var visited = new HashSet<string>();

        foreach (var nodeId in nodes.Keys)
        {
            distances[nodeId] = float.MaxValue;
        }
        distances[fromNodeId] = 0;
        priorityQueue.Enqueue(fromNodeId, 0);

        while (priorityQueue.Count > 0)
        {
            var currentNodeId = priorityQueue.Dequeue();

            if (visited.Contains(currentNodeId))
                continue;

            visited.Add(currentNodeId);

            if (currentNodeId == toNodeId)
            {
                var path = new List<string>();
                var current = toNodeId;

                while (current != fromNodeId)
                {
                    path.Add(current);
                    if (!previous.ContainsKey(current))
                        break;
                    current = previous[current];
                }
                path.Add(fromNodeId);
                path.Reverse();

                return new Path
                {
                    Nodes = path,
                    TotalDistance = distances[toNodeId]
                };
            }

            if (!adjacencyList.ContainsKey(currentNodeId))
                continue;

            foreach (var edge in adjacencyList[currentNodeId])
            {
                if (visited.Contains(edge.To))
                    continue;

                var newDistance = distances[currentNodeId] + edge.Length;

                if (newDistance < distances[edge.To])
                {
                    distances[edge.To] = newDistance;
                    previous[edge.To] = currentNodeId;
                    priorityQueue.Enqueue(edge.To, newDistance);
                }
            }
        }

        return null;
    }

}

public class Node
{
    public string Id { get; set; } = string.Empty;
    public float PosX { get; set; }
    public float PosY { get; set; }
    public string? ZoneId { get; set; }
    public ZoneDto? Zone { get; set; }
    public bool HasChargingStation { get; set; }
    public ChargingStationDto? ChargingStation { get; set; }
}

public class Edge
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public float Length { get; set; }
}

public class Path
{
    public List<string> Nodes { get; set; } = new();
    public float TotalDistance { get; set; }
}

public class ChargingPath
{
    public Path Path { get; set; } = null!;
    public List<string> ChargingStops { get; set; } = [];
    public float TotalCost { get; set; }
}

// Helper extension methods
public static class CustomerExtensions
{
    private static readonly Dictionary<string, float> EnergyConsumptionRates = new()
    {
        { "Delivery", 0.12f },
        { "Service", 0.15f },
        { "Personal", 0.10f }
    };

    public static float EnergyConsumptionPerKm(this CustomerDto customer)
    {
        return EnergyConsumptionRates.GetValueOrDefault(customer.Type, 0.12f);
    }
}
