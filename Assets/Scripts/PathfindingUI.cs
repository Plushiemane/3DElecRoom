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

    [Header("Visual Settings")]
    public Material LineMaterial;
    public float LineHeight = 0.1f;
    public float LineWidth = 0.3f;
    public Color PanelToRack1Color = Color.cyan;
    public Color Rack1ToRack2Color = Color.green;
    public Color Rack2ToPanelColor = Color.yellow;
    public Color BoundingBoxColor = new Color(1f, 0.5f, 0f, 0.3f);
    public Color ConnectionColor = new Color(0.5f, 0.5f, 1f, 0.2f);
    public Color GridColor = new Color(0.3f, 0.3f, 0.3f, 0.1f);

    [Header("Pathfinding Settings")]
    public float UpdateFrequency = 0.5f;
    public float SafeDistance = 2f;
    public float MaxConnectionDistance = 5f; // Reduced for better pathfinding
    public bool ShowBoundingBox = true;
    public bool ShowConnections = false;
    public bool ShowGrid = false;
    public int GridResolution = 10;

    [Header("Manual Overrides")]
    public Transform ManualDashboard;
    public List<Transform> ManualRacks = new List<Transform>();

    private List<SmartPathNode> nodes = new List<SmartPathNode>();
    private Dictionary<string, SmartPathNode> nodeMap = new Dictionary<string, SmartPathNode>();
    private List<GameObject> visualObjects = new List<GameObject>();
    private SmartPathNode panelNode;
    private SmartPathNode rack1Node;
    private SmartPathNode rack2Node;
    
    private Bounds racksBounds;
    private List<Vector3> boundingBoxCorners = new List<Vector3>();
    private List<SmartPathNode> boundaryNodes = new List<SmartPathNode>();
    private List<SmartPathNode> gridNodes = new List<SmartPathNode>();
    
    private float lastUpdateTime = 0f;
    private bool isRealtime = false;
    private Transform playerTransform;
    private Vector3 lastPlayerPosition;

    void Start()
    {
        if (VisualizeButton != null)
            VisualizeButton.onClick.AddListener(OnVisualizePath);
        
        if (RealtimeToggle != null)
            RealtimeToggle.onValueChanged.AddListener(OnRealtimeToggleChanged);
        
        if (DebugButton != null)
            DebugButton.onClick.AddListener(OnDebugConnections);

        InitializeSystem();
        FindPlayer();
        
        UpdateStatus("System initialized. Select racks and click Visualize.");
    }

    void InitializeSystem()
    {
        LoadAllNodes();
        FindPanelNode();
        CalculateRacksBoundingBox();
        CreateGridNodes(); // Create a grid for pathfinding
        GenerateNavigationGrid();
        FillDropdowns();
        
        LogConnectivityInfo();
    }

    void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            lastPlayerPosition = playerTransform.position;
            Debug.Log($"Player found at: {playerTransform.position}");
        }
    }

    void LoadAllNodes()
    {
        nodes.Clear();
        nodeMap.Clear();
        boundaryNodes.Clear();
        gridNodes.Clear();

        // Use manual racks if provided
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
        // Find objects that look like racks
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

        // Expand bounds
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
                
                // Skip if too close to a rack
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
        // Clear all connections
        foreach (var node in nodes)
        {
            node.Neighbors.Clear();
        }

        // Connect grid nodes in a proper grid pattern
        ConnectGridNodes();

        // Connect racks to nearest grid nodes
        foreach (var rack in nodes.Where(n => n.Type == SmartPathNode.NodeType.Rack))
        {
            ConnectToNearestGridNodes(rack, 2);
        }

        // Connect panel to nearest grid nodes
        if (panelNode != null)
        {
            ConnectToNearestGridNodes(panelNode, 2);
        }

        UpdateStatus($"Grid: {nodes.Count} nodes ({gridNodes.Count} grid nodes)");
    }

    void ConnectGridNodes()
    {
        // Organize grid nodes by position
        var gridByCoord = new Dictionary<Vector2Int, SmartPathNode>();
        float cellSize = Mathf.Max(racksBounds.size.x, racksBounds.size.z) / GridResolution;
        
        foreach (var node in gridNodes)
        {
            int gridX = Mathf.RoundToInt((node.Position.x - racksBounds.min.x) / cellSize);
            int gridZ = Mathf.RoundToInt((node.Position.z - racksBounds.min.z) / cellSize);
            gridByCoord[new Vector2Int(gridX, gridZ)] = node;
        }

        // Connect grid nodes to their neighbors (4-directional)
        foreach (var kvp in gridByCoord)
        {
            var coord = kvp.Key;
            var node = kvp.Value;

            // Check all 4 directions
            Vector2Int[] directions = {
                new Vector2Int(1, 0),  // right
                new Vector2Int(-1, 0), // left
                new Vector2Int(0, 1),  // forward
                new Vector2Int(0, -1)  // back
            };

            foreach (var dir in directions)
            {
                var neighborCoord = coord + dir;
                if (gridByCoord.TryGetValue(neighborCoord, out var neighbor))
                {
                    float distance = Vector3.Distance(node.Position, neighbor.Position);
                    if (distance <= cellSize * 1.5f) // Allow diagonal-ish connections
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
        
        CalculateAndVisualizeOptimalPath();
        
        UpdateStatus($"Visualizing: Dashboard → {rack1Name} → {rack2Name} → Dashboard");
    }

    void CalculateAndVisualizeOptimalPath()
    {
        if (panelNode == null || rack1Node == null || rack2Node == null) return;

        // Use A* for proper pathfinding
        var path1 = FindPathAStar(panelNode, rack1Node);
        var path2 = FindPathAStar(rack1Node, rack2Node);
        var path3 = FindPathAStar(rack2Node, panelNode);

        if (path1 == null || path2 == null || path3 == null)
        {
            UpdateStatus("WARNING: Could not find complete path. Check connections.");
            return;
        }

        DrawPathLines(path1, PanelToRack1Color, "Dashboard_to_Rack1");
        DrawPathLines(path2, Rack1ToRack2Color, "Rack1_to_Rack2");
        DrawPathLines(path3, Rack2ToPanelColor, "Rack2_to_Dashboard");

        // Draw waypoints for debugging
        DrawPathWaypoints(path1, PanelToRack1Color);
        DrawPathWaypoints(path2, Rack1ToRack2Color);
        DrawPathWaypoints(path3, Rack2ToPanelColor);

        UpdateStatus($"Path calculated: {path1.Count + path2.Count + path3.Count - 3} segments");
    }

    List<SmartPathNode> FindPathAStar(SmartPathNode start, SmartPathNode goal)
    {
        var openSet = new HashSet<SmartPathNode> { start };
        var closedSet = new HashSet<SmartPathNode>();
        var cameFrom = new Dictionary<SmartPathNode, SmartPathNode>();
        var gScore = new Dictionary<SmartPathNode, float>();
        var fScore = new Dictionary<SmartPathNode, float>();

        gScore[start] = 0;
        fScore[start] = Heuristic(start, goal);

        while (openSet.Count > 0)
        {
            var current = openSet.OrderBy(n => fScore.ContainsKey(n) ? fScore[n] : float.MaxValue).First();

            if (current == goal)
            {
                return ReconstructPath(cameFrom, current);
            }

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

        Debug.LogWarning($"No path found from {start.Name} to {goal.Name}");
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
                        LineWidth * 0.05f
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
            DrawSingleLine(start, end, BoundingBoxColor, $"BBox_{i}", LineWidth * 0.2f);
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
                    
                    DrawSingleLine(
                        new Vector3(node.Position.x, LineHeight + 0.01f, node.Position.z),
                        new Vector3(neighbor.Position.x, LineHeight + 0.01f, neighbor.Position.z),
                        connColor,
                        $"Conn_{node.Name}_{neighbor.Name}",
                        LineWidth * 0.08f
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
        
        if (LineMaterial == null)
        {
            LineMaterial = new Material(Shader.Find("Unlit/Color"));
        }
        
        lineRenderer.material = LineMaterial;
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
        
        // Add a glow effect with a second, wider line
        GameObject glowObj = new GameObject(lineName + "_Glow");
        glowObj.transform.SetParent(transform);
        LineRenderer glowRenderer = glowObj.AddComponent<LineRenderer>();
        glowRenderer.material = new Material(Shader.Find("Unlit/Color"));
        glowRenderer.startColor = new Color(color.r, color.g, color.b, 0.3f);
        glowRenderer.endColor = new Color(color.r, color.g, color.b, 0.3f);
        glowRenderer.startWidth = LineWidth * 2f;
        glowRenderer.endWidth = LineWidth * 2f;
        glowRenderer.positionCount = path.Count;
        glowRenderer.SetPositions(positions);
        
        visualObjects.Add(lineObj);
        visualObjects.Add(glowObj);
    }

    void DrawPathWaypoints(List<SmartPathNode> path, Color color)
    {
        for (int i = 0; i < path.Count; i++)
        {
            float size = 0.2f;
            if (i == 0 || i == path.Count - 1) size = 0.3f; // Start/end markers
            
            DrawSphere(
                new Vector3(path[i].Position.x, LineHeight + 0.1f, path[i].Position.z),
                color,
                size,
                $"Waypoint_{path[i].Name}_{i}"
            );
        }
    }

    void DrawSingleLine(Vector3 start, Vector3 end, Color color, string name, float width)
    {
        GameObject lineObj = new GameObject(name);
        lineObj.transform.SetParent(transform);
        
        LineRenderer lineRenderer = lineObj.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Unlit/Color"));
        lineRenderer.material.color = color;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
        
        visualObjects.Add(lineObj);
    }

    void DrawSphere(Vector3 position, Color color, float size, string name)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * size;
        sphere.transform.SetParent(transform);
        
        Renderer rend = sphere.GetComponent<Renderer>();
        rend.material = new Material(Shader.Find("Standard"));
        rend.material.color = color;
        
        // Remove collider to avoid interference
        Destroy(sphere.GetComponent<Collider>());
        
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

    void OnRealtimeToggleChanged(bool isOn)
    {
        isRealtime = isOn;
        UpdateStatus(isOn ? "Realtime: ON" : "Realtime: OFF");
    }

    void OnDebugConnections()
    {
        LogConnectivityInfo();
        ClearVisuals();
        DrawGrid();
        DrawAllConnections();
        
        // Mark different node types
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
                case SmartPathNode.NodeType.Grid:
                    markerColor = Color.green * 0.7f;
                    size = 0.1f;
                    break;
            }
            
            DrawSphere(
                new Vector3(node.Position.x, LineHeight + 0.15f, node.Position.z),
                markerColor,
                size,
                $"Debug_{node.Name}"
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
    public enum NodeType { Rack, Panel, Boundary, Grid, Waypoint }
    
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