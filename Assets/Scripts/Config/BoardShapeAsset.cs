using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inspector-editable board shape. Subclass this to add new shapes and they
/// immediately become assignable on any ModeConfig.
/// </summary>
public abstract class BoardShapeAsset : ScriptableObject, IBoardShape
{
    public abstract IEnumerable<Vector2Int> Cells();
}
