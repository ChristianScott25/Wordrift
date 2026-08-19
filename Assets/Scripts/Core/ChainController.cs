using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pointer input (touch or mouse) that builds a chain of adjacent tiles while
/// dragging. Dragging back onto the previous tile un-selects the last one.
///
/// Knows nothing about scoring or game modes — it just reports the chain.
/// </summary>
public class ChainController : MonoBehaviour
{
    [Header("Line")]
    [SerializeField] private LineRenderer line;
    [SerializeField] private Color lineColor = new Color(1f, 0.75f, 0.1f, 0.85f);
    [SerializeField] private float lineWidth = 0.12f;

    [Header("Rules")]
    [Tooltip("Allow diagonal connections between tiles.")]
    [SerializeField] private bool allowDiagonals = true;

    public bool InputEnabled { get; set; } = true;

    /// <summary>Fired whenever the chain grows or shrinks.</summary>
    public event Action<IReadOnlyList<Tile>> ChainChanged;

    /// <summary>Fired on release, with the finished chain.</summary>
    public event Action<IReadOnlyList<Tile>> ChainSubmitted;

    private Board board;
    private Camera cam;
    private readonly List<Tile> chain = new();
    private bool dragging;

    public void Init(Board board, Camera cam)
    {
        this.board = board;
        this.cam = cam;

        if (line == null) line = GetComponent<LineRenderer>();
        if (line != null)
        {
            line.startColor = line.endColor = lineColor;
            line.startWidth = line.endWidth = lineWidth;
            line.positionCount = 0;
        }
    }

    private void Update()
    {
        var pointer = Pointer.current;
        if (pointer == null || board == null) return;

        if (!InputEnabled || board.Busy)
        {
            if (dragging) CancelChain();
            return;
        }

        Vector3 worldPos = cam.ScreenToWorldPoint(pointer.position.ReadValue());
        worldPos.z = 0f;

        if (pointer.press.isPressed)
        {
            dragging = true;
            var tile = board.TileAt(worldPos);
            if (tile != null) TryAddOrBacktrack(tile);
            UpdateLine(worldPos);
        }
        else if (dragging)
        {
            dragging = false;
            Submit();
        }
    }

    private void TryAddOrBacktrack(Tile tile)
    {
        if (chain.Count == 0)
        {
            AddTile(tile);
            return;
        }

        // Dragging back to the second-to-last tile removes the last one.
        if (chain.Count >= 2 && tile == chain[chain.Count - 2])
        {
            chain[chain.Count - 1].SetSelected(false);
            chain.RemoveAt(chain.Count - 1);
            ChainChanged?.Invoke(chain);
            return;
        }

        if (chain.Contains(tile)) return;                 // each tile only once
        if (!IsConnectable(chain[chain.Count - 1], tile)) return;

        AddTile(tile);
    }

    private bool IsConnectable(Tile from, Tile to)
    {
        if (allowDiagonals) return Board.AreAdjacent(from.Cell, to.Cell);
        var delta = to.Cell - from.Cell;
        return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1;
    }

    private void AddTile(Tile tile)
    {
        chain.Add(tile);
        tile.SetSelected(true);
        ChainChanged?.Invoke(chain);
    }

    private void UpdateLine(Vector3 pointerWorld)
    {
        if (line == null) return;
        if (chain.Count == 0)
        {
            line.positionCount = 0;
            return;
        }
        line.positionCount = chain.Count + 1;
        for (int i = 0; i < chain.Count; i++)
            line.SetPosition(i, chain[i].transform.position);
        line.SetPosition(chain.Count, pointerWorld);
    }

    private void Submit()
    {
        ChainSubmitted?.Invoke(new List<Tile>(chain));
        ClearChain();
    }

    public void CancelChain()
    {
        dragging = false;
        ClearChain();
        ChainChanged?.Invoke(chain);
    }

    private void ClearChain()
    {
        foreach (var tile in chain)
            if (tile != null) tile.SetSelected(false);
        chain.Clear();
        if (line != null) line.positionCount = 0;
    }

    /// <summary>The word currently spelled out by a chain.</summary>
    public static string WordOf(IReadOnlyList<Tile> tiles)
    {
        var chars = new char[tiles.Count];
        for (int i = 0; i < tiles.Count; i++) chars[i] = tiles[i].Letter;
        return new string(chars);
    }
}
