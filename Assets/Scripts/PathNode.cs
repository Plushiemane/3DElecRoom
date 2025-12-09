using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reprezentuje węzeł grafu powiązany z konkretnym GameObjectem Rack_backup.
/// </summary>
public class PathNode
{
    public string Name;
    public GameObject Obj;
    public List<PathNode> Neighbors = new List<PathNode>();

    // opcjonalne współrzędne gridu (jeśli masz 1..5)
    public int Row = -1;
    public int Col = -1;

    public PathNode(string name, GameObject obj)
    {
        Name = name;
        Obj = obj;
    }
}
