using UnityEngine;
using System;

[Serializable]
public class Edge
{
    public Vector2 start;
    public Vector2 end;

    public Edge(Vector2 start, Vector2 end)
    {
        this.start = start;
        this.end = end;
    }
}