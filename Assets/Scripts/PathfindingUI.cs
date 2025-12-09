using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Text.RegularExpressions;
using System;

public class SmartPathVisualizer : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Dropdown Rack1Dropdown;
    public TMP_Dropdown Rack2Dropdown;
    public Button VisualizeButton;
    public Toggle RealtimeToggle;
    public TMP_Text StatusText;
    public Button DebugButton;

    [Header("Player Reference")]
    public Transform PlayerTransform; // Drag your Player/MainCamera here

    [Header("Visual Settings")]
    public Material LineMaterial;
    public float LineHeight = 0.1f;
    public float LineWidth = 0.3f;
    public Color PanelToRack1Color = Color.cyan;
    public Color Rack1ToRack2Color = Color.green;
    public Color Rack2ToPanelColor = Color.yellow;
    public Color PlayerToRack1Color = Color.magenta;
    public Color BoundingBoxColor = new Color(1f, 0.5f, 0f, 0.3f);
    public Color ConnectionColor = new Color(0.5f, 0.5f, 1f, 0.2f);
    public Color GridColor = new Color(0.3f, 0.3f, 0.3f, 0.1f);

    [Header("Pathfinding Settings")]
    public float UpdateFrequency = 0.5f;
    public float SafeDistance = 2f;
    public float MaxConnectionDistance = 5f;
    public bool ShowBoundingBox = true;
    public bool ShowConnections = false;
    public bool ShowGrid = false;
    public int GridResolution = 10;

    [Header("Manual Overrides")]
    public Transform ManualDashboard;
    public List<Transform> ManualRacks = new List<Transform>();

    [Header("Path Behavior Settings")]
    public float ProximityThreshold = 1.0f; // How close player needs to be to "reach" a point

    private List<SmartPathNode> nodes = new List<SmartPathNode>();
    private Dictionary<string, SmartPathNode> nodeMap = new Dictionary<string, SmartPathNode>();
    private List<GameObject> visualObjects = new List<GameObject>();
    private List<GameObject> pathVisuals = new List<GameObject>();
    private List<GameObject> debugVisuals = new List<GameObject>();
    
    private SmartPathNode panelNode;
    private SmartPathNode rack1Node;
    private SmartPathNode rack2Node;
    private SmartPathNode playerNode;
    
    private Bounds racksBounds;
    private List<Vector3> boundingBoxCorners = new List<Vector3>();
    private List<SmartPathNode> boundaryNodes = new List<SmartPathNode>();
    private List<SmartPathNode> gridNodes = new List<SmartPathNode>();
    
    private float lastUpdateTime = 0f;
    private bool isRealtime = false;
    private Vector3 lastPlayerPosition;

    private PathState currentPathState = PathState.NotStarted;
    private enum PathState
    {
        NotStarted,      // No path active
        ToFirstRack,     // Going to first rack
        ToSecondRack,    // Going to second rack
        BackToDashboard, // Returning to dashboard
        Complete         // Mission complete
    }

    void Start()
    {
        if (VisualizeButton != null)
            VisualizeButton.onClick.AddListener(OnVisualizePath);
        
        if (RealtimeToggle != null)
            RealtimeToggle.onValueChanged.AddListener(OnRealtimeToggleChanged);
        
        if (DebugButton != null)
            DebugButton.onClick.AddListener(OnDebugConnections);

        InitializeSystem();
        
        UpdateStatus("System initialized. Select racks and click Visualize.");
    }

    void InitializeSystem()
    {
        LoadAllNodes();
        FindPanelNode();
        CreatePlayerNode();
        CalculateRacksBoundingBox();
        CreateGridNodes();
        GenerateNavigationGrid();
        FillDropdowns();
        
        LogConnectivityInfo();
    }

    void CreatePlayerNode()
    {
        playerNode = new SmartPathNode("Player", null, SmartPathNode.NodeType.Player);
        
        if (PlayerTransform != null)
        {
            playerNode.Position = PlayerTransform.position;
            lastPlayerPosition = PlayerTransform.position;
            Debug.Log($"Player assigned at: {playerNode.Position}");
        }
        else
        {
            playerNode.Position = Vector3.zero;
            lastPlayerPosition = Vector3.zero;
            Debug.LogWarning("Player Transform not assigned. Using origin.");
        }
        
        playerNode.OriginalPosition = playerNode.Position;
        nodes.Add(playerNode);
    }

    void LoadAllNodes()
    {
        nodes.Clear();
        nodeMap.Clear();
        boundaryNodes.Clear();
        gridNodes.Clear();

        if (ManualRacks != null && ManualRacks.Count > 0)
        {
            foreach (var rackTransform in ManualRacks)
            {
                if (rackTransform != null)
                {
                    var n = new SmartPathNode(rackTransform.name, rackTransform.gameObject, SmartPathNode.NodeType.Rack);
                    n.Position = rackTransform.position;
                    n.OriginalPosition = n.Position;
                    
                    nodes.Add(n);
                    nodeMap[n.Name] = n;
                }
            }
        }
        else
        {
            FindRacksAutomatically();
        }
    }

    void FindRacksAutomatically()
    {
        var allTransforms = FindObjectsOfType<Transform>();
        foreach (var t in allTransforms)
        {
            if (t.name.Contains("Rack") || t.name.Contains("rack"))
            {
                var n = new SmartPathNode(t.name, t.gameObject, SmartPathNode.NodeType.Rack);
                n.Position = t.position;
                n.OriginalPosition = n.Position;
                
                nodes.Add(n);
                nodeMap[n.Name] = n;
            }
        }
    }

    void FindPanelNode()
    {
        if (ManualDashboard != null)
        {
            panelNode = new SmartPathNode("Dashboard", ManualDashboard.gameObject, SmartPathNode.NodeType.Panel);
            panelNode.Position = ManualDashboard.position;
            panelNode.OriginalPosition = panelNode.Position;
            
            nodes.Add(panelNode);
            nodeMap[panelNode.Name] = panelNode;
            return;
        }

        GameObject dashboard = GameObject.Find("Dashboard");
        if (dashboard == null) dashboard = GameObject.Find("Panel");
        if (dashboard == null) dashboard = GameObject.Find("Canvas");

        if (dashboard != null)
        {
            panelNode = new SmartPathNode("Dashboard", dashboard, SmartPathNode.NodeType.Panel);
            panelNode.Position = dashboard.transform.position;
            panelNode.OriginalPosition = panelNode.Position;
            
            nodes.Add(panelNode);
            nodeMap[panelNode.Name] = panelNode;
        }
        else
        {
            panelNode = new SmartPathNode("Dashboard", null, SmartPathNode.NodeType.Panel);
            panelNode.Position = Vector3.zero;
            panelNode.OriginalPosition = panelNode.Position;
            
            nodes.Add(panelNode);
            nodeMap[panelNode.Name] = panelNode;
        }
    }

    void CalculateRacksBoundingBox()
    {
        var rackNodes = nodes.Where(n => n.Type == SmartPathNode.NodeType.Rack).ToList();
        if (rackNodes.Count == 0)
        {
            racksBounds = new Bounds(Vector3.zero, new Vector3(20, 0, 20));
            return;
        }

        racksBounds = new Bounds(rackNodes[0].Position, Vector3.zero);
        foreach (var node in rackNodes)
        {
            racksBounds.Encapsulate(node.Position);
        }

        racksBounds.Expand(SafeDistance * 2);

        boundingBoxCorners.Clear();
        boundingBoxCorners.Add(new Vector3(racksBounds.min.x, LineHeight, racksBounds.min.z));
        boundingBoxCorners.Add(new Vector3(racksBounds.max.x, LineHeight, racksBounds.min.z));
        boundingBoxCorners.Add(new Vector3(racksBounds.max.x, LineHeight, racksBounds.max.z));
        boundingBoxCorners.Add(new Vector3(racksBounds.min.x, LineHeight, racksBounds.max.z));
    }

    void CreateGridNodes()
    {
        if (racksBounds.size.magnitude < 0.1f) return;

        float cellSize = Mathf.Max(racksBounds.size.x, racksBounds.size.z) / GridResolution;
        
        for (int x = 0; x <= GridResolution; x++)
        {
            for (int z = 0; z <= GridResolution; z++)
            {
                float posX = racksBounds.min.x + (x * cellSize);
                float posZ = racksBounds.min.z + (z * cellSize);
                
                bool tooCloseToRack = false;
                foreach (var rack in nodes.Where(n => n.Type == SmartPathNode.NodeType.Rack))
                {
                    float distance = Vector2.Distance(
                        new Vector2(posX, posZ),
                        new Vector2(rack.Position.x, rack.Position.z)
                    );
                    if (distance < SafeDistance)
                    {
                        tooCloseToRack = true;
                        break;
                    }
                }
                
                if (tooCloseToRack) continue;

                Vector3 position = new Vector3(posX, LineHeight, posZ);
                string gridNodeName = $"Grid_{x}_{z}";
                
                var gridNode = new SmartPathNode(gridNodeName, null, SmartPathNode.NodeType.Grid);
                gridNode.Position = position;
                gridNode.OriginalPosition = position;
                
                gridNodes.Add(gridNode);
                nodes.Add(gridNode);
                nodeMap[gridNodeName] = gridNode;
            }
        }
    }

    void GenerateNavigationGrid()
    {
        foreach (var node in nodes)
        {
            node.Neighbors.Clear();
        }

        ConnectGridNodes();

        foreach (var rack in nodes.Where(n => n.Type == SmartPathNode.NodeType.Rack))
        {
            ConnectToNearestGridNodes(rack, 2);
        }

        if (panelNode != null)
        {
            ConnectToNearestGridNodes(panelNode, 2);
        }

        if (playerNode != null)
        {
            ConnectPlayerToGrid();
        }

        UpdateStatus($"Grid: {nodes.Count} nodes ({gridNodes.Count} grid nodes)");
    }

    void ConnectGridNodes()
    {
        var gridByCoord = new Dictionary<Vector2Int, SmartPathNode>();
        float cellSize = Mathf.Max(racksBounds.size.x, racksBounds.size.z) / GridResolution;
        
        foreach (var node in gridNodes)
        {
            int gridX = Mathf.RoundToInt((node.Position.x - racksBounds.min.x) / cellSize);
            int gridZ = Mathf.RoundToInt((node.Position.z - racksBounds.min.z) / cellSize);
            gridByCoord[new Vector2Int(gridX, gridZ)] = node;
        }

        foreach (var kvp in gridByCoord)
        {
            var coord = kvp.Key;
            var node = kvp.Value;

            Vector2Int[] directions = {
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, -1)
            };

            foreach (var dir in directions)
            {
                var neighborCoord = coord + dir;
                if (gridByCoord.TryGetValue(neighborCoord, out var neighbor))
                {
                    float distance = Vector3.Distance(node.Position, neighbor.Position);
                    if (distance <= cellSize * 1.5f)
                    {
                        node.Neighbors.Add(neighbor);
                        neighbor.Neighbors.Add(node);
                    }
                }
            }
        }
    }

    void ConnectToNearestGridNodes(SmartPathNode node, int count)
    {
        var nearestGridNodes = gridNodes
            .OrderBy(n => Vector3.Distance(node.Position, n.Position))
            .Take(count)
            .ToList();
        
        foreach (var gridNode in nearestGridNodes)
        {
            float distance = Vector3.Distance(node.Position, gridNode.Position);
            if (distance <= MaxConnectionDistance * 1.5f)
            {
                node.Neighbors.Add(gridNode);
                gridNode.Neighbors.Add(node);
            }
        }
    }

    void ConnectPlayerToGrid()
    {
        if (playerNode == null) return;
        
        // Clear player's existing connections
        foreach (var neighbor in new List<SmartPathNode>(playerNode.Neighbors))
        {
            playerNode.Neighbors.Remove(neighbor);
            neighbor.Neighbors.Remove(playerNode);
        }
        
        ConnectToNearestGridNodes(playerNode, 3);
    }

    void FillDropdowns()
    {
        if (Rack1Dropdown == null || Rack2Dropdown == null) return;

        Rack1Dropdown.ClearOptions();
        Rack2Dropdown.ClearOptions();

        var rackOptions = nodes
            .Where(n => n.Type == SmartPathNode.NodeType.Rack)
            .Select(n => n.Name)
            .ToList();

        if (rackOptions.Count == 0)
        {
            rackOptions.Add("No racks found");
        }

        Rack1Dropdown.AddOptions(rackOptions);
        Rack2Dropdown.AddOptions(rackOptions);

        if (rackOptions.Count > 1)
        {
            Rack1Dropdown.value = 0;
            Rack2Dropdown.value = 1;
        }
    }

    void Update()
    {
        if (!isRealtime || PlayerTransform == null) return;
        
        if (Time.time - lastUpdateTime >= UpdateFrequency)
        {
            UpdatePlayerPosition();
            UpdatePathState(); // Check if player reached points
            lastUpdateTime = Time.time;
        }
    }

    void UpdatePlayerPosition()
    {
        if (PlayerTransform == null) return;

        Vector3 currentPos = PlayerTransform.position;

        if (Vector3.Distance(currentPos, lastPlayerPosition) > 0.1f)
        {
            lastPlayerPosition = currentPos;

            if (playerNode != null)
            {
                playerNode.Position = currentPos;
                ConnectPlayerToGrid();

                // Refresh currently relevant path segment based on state
                ClearPathVisualsOnly();
                switch (currentPathState)
                {
                    case PathState.ToFirstRack:
                        CalculateAndVisualizeOptimalPath(); // draws first segment only in realtime
                        break;
                    case PathState.ToSecondRack:
                        ShowPathToSecondRackOnly();
                        break;
                    case PathState.BackToDashboard:
                        ShowPathBackToDashboardOnly();
                        break;
                    default:
                        // No active path
                        break;
                }
            }
        }
    }

    void UpdatePathState()
    {
        if (currentPathState == PathState.NotStarted || rack1Node == null || rack2Node == null)
            return;

        float distanceToFirstRack = Vector3.Distance(PlayerTransform.position, rack1Node.Position);
        float distanceToSecondRack = Vector3.Distance(PlayerTransform.position, rack2Node.Position);
        float distanceToDashboard = panelNode != null ? Vector3.Distance(PlayerTransform.position, panelNode.Position) : float.MaxValue;

        switch (currentPathState)
        {
            case PathState.ToFirstRack:
                if (distanceToFirstRack <= ProximityThreshold)
                {
                    // Player reached first rack - clear old path and show path to second rack
                    Debug.Log("Reached first rack! Updating path to second rack.");
                    currentPathState = PathState.ToSecondRack;
                    ClearPathVisualsOnly();
                    ShowPathToSecondRackOnly();
                    UpdateStatus("Reached Rack 1! Path updated to Rack 2");
                }
                break;
                
            case PathState.ToSecondRack:
                if (distanceToSecondRack <= ProximityThreshold)
                {
                    // Player reached second rack - clear old path and show path back to dashboard
                    Debug.Log("Reached second rack! Updating path back to dashboard.");
                    currentPathState = PathState.BackToDashboard;
                    ClearPathVisualsOnly();
                    ShowPathBackToDashboardOnly();
                    UpdateStatus("Reached Rack 2! Path updated back to Dashboard");
                }
                break;
                
            case PathState.BackToDashboard:
                if (panelNode != null && distanceToDashboard <= ProximityThreshold)
                {
                    // Player returned to dashboard - clear all lines
                    Debug.Log("Returned to dashboard! Clearing all paths.");
                    currentPathState = PathState.Complete;
                    ClearPathVisualsOnly();
                    UpdateStatus("Mission complete! All paths cleared.");
                    
                    // Reset for next visualization
                    currentPathState = PathState.NotStarted;
                }
                break;
        }
    }

    void ShowPathToSecondRackOnly()
    {
        if (rack1Node == null || rack2Node == null || playerNode == null) return;
        
        // Only draw path from current position to second rack
        playerNode.Position = PlayerTransform.position;
        ConnectPlayerToGrid();
        
        var pathToSecondRack = FindPathAStar(playerNode, rack2Node);
        if (pathToSecondRack != null)
        {
            DrawPathLines(pathToSecondRack, Rack1ToRack2Color, "Player_to_Rack2_Only");
            DrawPathWaypoints(pathToSecondRack, Rack1ToRack2Color);
        }
    }

    void ShowPathBackToDashboardOnly()
    {
        if (playerNode == null || panelNode == null) return;
        
        // Only draw path from current position back to dashboard
        playerNode.Position = PlayerTransform.position;
        ConnectPlayerToGrid();
        
        var pathToDashboard = FindPathAStar(playerNode, panelNode);
        if (pathToDashboard != null)
        {
            DrawPathLines(pathToDashboard, Rack2ToPanelColor, "Player_to_Dashboard_Only");
            DrawPathWaypoints(pathToDashboard, Rack2ToPanelColor);
        }
    }

    public void OnVisualizePath()
    {
        if (panelNode == null)
        {
            UpdateStatus("ERROR: Panel not found!");
            return;
        }

        var rack1Name = Rack1Dropdown.options[Rack1Dropdown.value].text;
        var rack2Name = Rack2Dropdown.options[Rack2Dropdown.value].text;

        if (rack1Name == "No racks found" || rack2Name == "No racks found")
        {
            UpdateStatus("ERROR: No racks available!");
            return;
        }

        if (!nodeMap.ContainsKey(rack1Name) || !nodeMap.ContainsKey(rack2Name))
        {
            UpdateStatus($"ERROR: Racks not found!");
            return;
        }

        rack1Node = nodeMap[rack1Name];
        rack2Node = nodeMap[rack2Name];

        // Reset state
        currentPathState = PathState.ToFirstRack;
        
        // Clear all visuals when manually visualizing
        ClearVisuals();
        
        if (ShowGrid)
        {
            DrawGrid();
        }
        
        if (ShowBoundingBox)
        {
            DrawBoundingBox();
        }
        
        if (ShowConnections)
        {
            DrawAllConnections();
        }
        
        // Start with full path visualization (will conditionally draw in realtime)
        CalculateAndVisualizeOptimalPath();
        
        UpdateStatus($"Started: {(isRealtime ? "Player" : "Dashboard")} → {rack1Name} → {rack2Name} → {(isRealtime ? "Player" : "Dashboard")}");
    }

    void CalculateAndVisualizeOptimalPath()
    {
        if (rack1Node == null || rack2Node == null) return;

        SmartPathNode startNode = isRealtime ? playerNode : panelNode;
        if (startNode == null)
        {
            UpdateStatus("ERROR: Start node not found!");
            return;
        }

        Debug.Log($"Calculating paths from {startNode.Name} at {startNode.Position}");

        Color firstSegmentColor = isRealtime ? PlayerToRack1Color : PanelToRack1Color;
        string firstSegmentName = isRealtime ? "Player_to_Rack1" : "Dashboard_to_Rack1";
        Color lastSegmentColor = isRealtime ? PlayerToRack1Color : Rack2ToPanelColor;
        string lastSegmentName = isRealtime ? "Rack2_to_Player" : "Rack2_to_Dashboard";
        
        var path1 = FindPathAStar(startNode, rack1Node);
        var path2 = FindPathAStar(rack1Node, rack2Node);
        var path3 = FindPathAStar(rack2Node, startNode);

        if (path1 == null || path2 == null || path3 == null)
        {
            UpdateStatus("WARNING: Could not find complete path.");
            return;
        }

        // Only draw the first segment initially if we're in realtime mode
        if (isRealtime && currentPathState == PathState.ToFirstRack)
        {
            ClearPathVisualsOnly();
            DrawPathLines(path1, firstSegmentColor, firstSegmentName);
            DrawPathWaypoints(path1, firstSegmentColor);
            UpdateStatus($"Following path to Rack 1... (Get within {ProximityThreshold}m)");
        }
        else if (!isRealtime)
        {
            // Non-realtime mode shows all paths
            ClearPathVisualsOnly();
            DrawPathLines(path1, firstSegmentColor, firstSegmentName);
            DrawPathLines(path2, Rack1ToRack2Color, "Rack1_to_Rack2");
            DrawPathLines(path3, lastSegmentColor, lastSegmentName);

            DrawPathWaypoints(path1, firstSegmentColor);
            DrawPathWaypoints(path2, Rack1ToRack2Color);
            DrawPathWaypoints(path3, lastSegmentColor);

            UpdateStatus($"Path calculated: {path1.Count + path2.Count + path3.Count - 3} segments");
        }
    }

    // Add back missing pathfinding helpers (A*)
    List<SmartPathNode> FindPathAStar(SmartPathNode start, SmartPathNode goal)
    {
        if (start == null || goal == null) return null;

        var openSet = new HashSet<SmartPathNode> { start };
        var closedSet = new HashSet<SmartPathNode>();
        var cameFrom = new Dictionary<SmartPathNode, SmartPathNode>();
        var gScore = new Dictionary<SmartPathNode, float>();
        var fScore = new Dictionary<SmartPathNode, float>();

        gScore[start] = 0f;
        fScore[start] = Heuristic(start, goal);

        while (openSet.Count > 0)
        {
            var current = openSet.OrderBy(n => fScore.ContainsKey(n) ? fScore[n] : float.MaxValue).First();

            if (current == goal)
                return ReconstructPath(cameFrom, current);

            openSet.Remove(current);
            closedSet.Add(current);

            foreach (var neighbor in current.Neighbors)
            {
                if (closedSet.Contains(neighbor)) continue;

                float tentativeGScore = (gScore.ContainsKey(current) ? gScore[current] : float.MaxValue)
                    + Vector3.Distance(current.Position, neighbor.Position);

                if (!openSet.Contains(neighbor))
                {
                    openSet.Add(neighbor);
                }
                else if (tentativeGScore >= (gScore.ContainsKey(neighbor) ? gScore[neighbor] : float.MaxValue))
                {
                    continue;
                }

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeGScore;
                fScore[neighbor] = gScore[neighbor] + Heuristic(neighbor, goal);
            }
        }

        return null;
    }

    float Heuristic(SmartPathNode a, SmartPathNode b)
    {
        return Vector3.Distance(a.Position, b.Position);
    }

    List<SmartPathNode> ReconstructPath(Dictionary<SmartPathNode, SmartPathNode> cameFrom, SmartPathNode current)
    {
        var path = new List<SmartPathNode> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }
        return path;
    }

    void OnRealtimeToggleChanged(bool isOn)
    {
        isRealtime = isOn;
        currentPathState = PathState.NotStarted; // Reset state when toggling
        
        UpdateStatus(isOn ? "Realtime: ON (Dynamic paths)" : "Realtime: OFF (Static paths)");
        
        if (rack1Node != null && rack2Node != null)
        {
            ClearPathVisualsOnly();
            CalculateAndVisualizeOptimalPath();
        }
    }

    void DrawGrid()
    {
        foreach (var node in gridNodes)
        {
            foreach (var neighbor in node.Neighbors)
            {
                if (node.GetHashCode() < neighbor.GetHashCode())
                {
                    DrawSingleLine(
                        new Vector3(node.Position.x, LineHeight + 0.02f, node.Position.z),
                        new Vector3(neighbor.Position.x, LineHeight + 0.02f, neighbor.Position.z),
                        GridColor,
                        $"Grid_{node.Name}_{neighbor.Name}",
                        LineWidth * 0.05f,
                        isDebug: true
                    );
                }
            }
        }
    }

    void DrawBoundingBox()
    {
        if (boundingBoxCorners.Count < 4) return;

        for (int i = 0; i < 4; i++)
        {
            Vector3 start = boundingBoxCorners[i];
            Vector3 end = boundingBoxCorners[(i + 1) % 4];
            DrawSingleLine(start, end, BoundingBoxColor, $"BBox_{i}", LineWidth * 0.2f, isDebug: true);
        }
    }

    void DrawAllConnections()
    {
        foreach (var node in nodes)
        {
            foreach (var neighbor in node.Neighbors)
            {
                if (node.GetHashCode() < neighbor.GetHashCode())
                {
                    Color connColor = ConnectionColor;
                    if (node.Type == SmartPathNode.NodeType.Rack || neighbor.Type == SmartPathNode.NodeType.Rack)
                        connColor = Color.red * 0.5f;
                    else if (node.Type == SmartPathNode.NodeType.Panel || neighbor.Type == SmartPathNode.NodeType.Panel)
                        connColor = Color.blue * 0.5f;
                    else if (node.Type == SmartPathNode.NodeType.Player || neighbor.Type == SmartPathNode.NodeType.Player)
                        connColor = Color.magenta * 0.5f;
                    
                    DrawSingleLine(
                        new Vector3(node.Position.x, LineHeight + 0.01f, node.Position.z),
                        new Vector3(neighbor.Position.x, LineHeight + 0.01f, neighbor.Position.z),
                        connColor,
                        $"Conn_{node.Name}_{neighbor.Name}",
                        LineWidth * 0.08f,
                        isDebug: true
                    );
                }
            }
        }
    }

        void DrawPathLines(List<SmartPathNode> path, Color color, string lineName)
    {
        if (path == null || path.Count < 2) return;

        GameObject lineObj = new GameObject(lineName);
        lineObj.transform.SetParent(transform);
        
        LineRenderer lineRenderer = lineObj.AddComponent<LineRenderer>();
        
        // Create a fresh material with Unlit/Color shader for EACH line
        Material lineMaterial = new Material(Shader.Find("Unlit/Color"));
        lineMaterial.color = color; // Set the material color directly
        lineRenderer.material = lineMaterial;
        
        // Also set the line renderer colors
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.startWidth = LineWidth;
        lineRenderer.endWidth = LineWidth;
        lineRenderer.positionCount = path.Count;
        
        Vector3[] positions = new Vector3[path.Count];
        for (int i = 0; i < path.Count; i++)
        {
            positions[i] = new Vector3(path[i].Position.x, LineHeight, path[i].Position.z);
        }
        lineRenderer.SetPositions(positions);
        
        // Store in path visuals list
        pathVisuals.Add(lineObj);
        visualObjects.Add(lineObj);
        
        Debug.Log($"Drawn path '{lineName}' with color {color}");
    }

    void DrawPathWaypoints(List<SmartPathNode> path, Color color)
    {
        for (int i = 0; i < path.Count; i++)
        {
            float size = 0.2f;
            if (i == 0 || i == path.Count - 1) size = 0.3f;
            
            DrawSphere(
                new Vector3(path[i].Position.x, LineHeight + 0.1f, path[i].Position.z),
                color,
                size,
                $"Waypoint_{path[i].Name}_{i}",
                isPath: true
            );
        }
    }

    void DrawSingleLine(Vector3 start, Vector3 end, Color color, string name, float width, bool isDebug = false)
    {
        GameObject lineObj = new GameObject(name);
        lineObj.transform.SetParent(transform);
        
        LineRenderer lineRenderer = lineObj.AddComponent<LineRenderer>();
        
        // Create material with Unlit/Color shader
        Material lineMaterial = new Material(Shader.Find("Unlit/Color"));
        lineMaterial.color = color;
        lineRenderer.material = lineMaterial;
        
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
        
        // Store in appropriate list
        if (isDebug)
        {
            debugVisuals.Add(lineObj);
        }
        else
        {
            pathVisuals.Add(lineObj);
        }
        
        visualObjects.Add(lineObj);
    }

    void DrawSphere(Vector3 position, Color color, float size, string name, bool isPath = false)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * size;
        sphere.transform.SetParent(transform);
        
        Renderer rend = sphere.GetComponent<Renderer>();
        
        // For spheres, use a more visible material
        Material sphereMaterial = new Material(Shader.Find("Standard"));
        sphereMaterial.color = color;
        
        // Enable emission for better visibility
        sphereMaterial.EnableKeyword("_EMISSION");
        sphereMaterial.SetColor("_EmissionColor", color * 0.5f);
        rend.material = sphereMaterial;
        
        Destroy(sphere.GetComponent<Collider>());
        
        // Store in appropriate list
        if (isPath)
        {
            pathVisuals.Add(sphere);
        }
        else
        {
            debugVisuals.Add(sphere);
        }
        
        visualObjects.Add(sphere);
    }

    void ClearVisuals()
    {
        foreach (var obj in visualObjects)
        {
            if (obj != null)
                Destroy(obj);
        }
        visualObjects.Clear();
        pathVisuals.Clear();
        debugVisuals.Clear();
        Debug.Log("Cleared all visuals");
    }

    void ClearPathVisualsOnly()
    {
        // Clear only path visuals
        foreach (var obj in pathVisuals)
        {
            if (obj != null)
            {
                if (visualObjects.Contains(obj))
                {
                    visualObjects.Remove(obj);
                }
                Destroy(obj);
            }
        }
        pathVisuals.Clear();
        Debug.Log("Cleared path visuals only");
    }

    void ClearDebugVisualsOnly()
    {
        // Clear only debug visuals
        foreach (var obj in debugVisuals)
        {
            if (obj != null)
            {
                if (visualObjects.Contains(obj))
                {
                    visualObjects.Remove(obj);
                }
                Destroy(obj);
            }
        }
        debugVisuals.Clear();
        Debug.Log("Cleared debug visuals only");
    }

    void LogConnectivityInfo()
    {
        Debug.Log("=== CONNECTIVITY REPORT ===");
        Debug.Log($"Total nodes: {nodes.Count}");
        Debug.Log($"Grid nodes: {gridNodes.Count}");
        Debug.Log($"Rack nodes: {nodes.Count(n => n.Type == SmartPathNode.NodeType.Rack)}");
        
        foreach (var node in nodes)
        {
            Debug.Log($"{node.Name} ({node.Type}): {node.Neighbors.Count} connections");
        }
        Debug.Log("===========================");
    }

    void OnDebugConnections()
    {
        LogConnectivityInfo();
        ClearVisuals(); // Clear everything for debug view
        DrawGrid();
        DrawAllConnections();
        
        foreach (var node in nodes)
        {
            Color markerColor = Color.gray;
            float size = 0.2f;
            
            switch (node.Type)
            {
                case SmartPathNode.NodeType.Rack:
                    markerColor = Color.red;
                    size = 0.4f;
                    break;
                case SmartPathNode.NodeType.Panel:
                    markerColor = Color.blue;
                    size = 0.5f;
                    break;
                case SmartPathNode.NodeType.Player:
                    markerColor = Color.magenta;
                    size = 0.6f;
                    break;
                case SmartPathNode.NodeType.Grid:
                    markerColor = Color.green * 0.7f;
                    size = 0.1f;
                    break;
            }
            
            DrawSphere(
                new Vector3(node.Position.x, LineHeight + 0.15f, node.Position.z),
                markerColor,
                size,
                $"Debug_{node.Name}",
                isPath: false
            );
        }
        
        UpdateStatus("Debug: Showing grid and connections");
    }

    void UpdateStatus(string message)
    {
        Debug.Log($"[PathVisualizer] {message}");
        
        if (StatusText != null)
            StatusText.text = message;
    }

    void OnDestroy()
    {
        ClearVisuals();
    }

    public void RefreshSystem()
    {
        ClearVisuals();
        InitializeSystem();
        UpdateStatus("System refreshed");
    }
}

public class SmartPathNode
{
    public enum NodeType { Rack, Panel, Boundary, Grid, Waypoint, Player }
    
    public string Name;
    public GameObject Obj;
    public NodeType Type;
    public Vector3 Position;
    public Vector3 OriginalPosition;
    public List<SmartPathNode> Neighbors = new List<SmartPathNode>();

    public SmartPathNode(string name, GameObject obj, NodeType type)
    {
        Name = name;
        Obj = obj;
        Type = type;
        if (obj != null)
        {
            Position = obj.transform.position;
            OriginalPosition = Position;
        }
    }
}