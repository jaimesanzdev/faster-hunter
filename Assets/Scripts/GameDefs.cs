using UnityEngine;

/// <summary>
/// Identifies the shape type of a game object.
/// Used to determine which shapes match the current round rule.
/// </summary>
public enum ShapeId { Circle = 0, Square = 1, Triangle = 2, Hexagon = 3, Pentagon = 4, Diamond = 5, Trapezoid = 6 }

/// <summary>
/// Defines the type of rule that players must follow in a given round.
/// </summary>
public enum RuleKind
{
    FindByShape = 0,         // Players must find shapes matching a specific shape type
    FindByColor = 1,         // Players must find shapes matching a specific color
    FindByShapeAndColor = 2  // Players must find shapes matching both a specific shape and color
}

/// <summary>
/// Centralized repository of shared game constants.
/// Contains the color palette and color name mappings used by all shape entities.
/// </summary>
public static class GameDefs
{
    /// <summary>
    /// The full set of colors that can be assigned to shapes.
    /// Indices match those in <see cref="ColorNames"/>.
    /// </summary>
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

    /// <summary>
    /// Human-readable names for each color in <see cref="Palette"/>.
    /// Used to build the round rule text displayed to players.
    /// </summary>
    public static readonly string[] ColorNames = new string[]
    {
        "Red","Orange","Yellow","Green","Cyan","Blue","Purple","Pink","White","Lime"
    };
}