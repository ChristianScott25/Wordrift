using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines which grid cells exist. The board never assumes a rectangle — any
/// set of coordinates works (holes, L-shapes, diamonds, whatever). Implement
/// this (or subclass BoardShapeAsset for an inspector-editable version) to add
/// new shapes.
/// </summary>
public interface IBoardShape
{
    IEnumerable<Vector2Int> Cells();
}
