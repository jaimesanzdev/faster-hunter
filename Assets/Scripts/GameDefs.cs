using UnityEngine;

public enum ShapeId { Circle = 0, Square = 1, Triangle = 2, Hexagon = 3, Pentagon = 4, Diamond = 5, Trapezoid = 6 }
public enum RuleKind { FindByShape = 0, FindByColor = 1, FindByShapeAndColor = 2 }

public static class GameDefs
{
    public static readonly Color[] Palette = new Color[]
    {
        new Color(1f, 0.10f, 0.10f), // Red
        new Color(1f, 0.55f, 0f),    // Orange
        new Color(1f, 0.92f, 0f),    // Yellow
        new Color(0.2f, 1f, 0.2f),   // Green
        new Color(0f, 0.9f, 0.9f),   // Cyan
        new Color(0.2f, 0.4f, 1f),   // Blue
        new Color(0.65f, 0.25f, 1f), // Purple
        new Color(1f, 0.25f, 0.7f),  // Pink
        new Color(1f,1f,1f),         // White
        new Color(0.7f,1f,0f)        // Lime
    };

    public static readonly string[] ColorNames = new string[]
    {
        "Red","Orange","Yellow","Green","Cyan","Blue","Purple","Pink","White","Lime"
    };
}