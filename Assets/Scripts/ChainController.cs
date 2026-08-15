using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pointer input (touch or mouse via the Input System) that builds a chain of
/// adjacent tiles while dragging. Dragging back onto the previous tile
/// un-selects the last one. Releasing submits the chain to the GameManager.
/// </summary>
public class ChainController : MonoBehaviour
{
    public bool InputEnabled { get; set; } = true;

    private GameManager game;
    private Board board;
    private Camera cam;
    private LineRenderer line;

    private readonly List<Tile> chain = new();
    private bool dragging;

    public void Init(GameManager game, Board board, Camera cam)
    {
        this.game = game;
        this.board = board;
        this.cam = cam;

        line = gameObject.AddComponent<LineRenderer>();
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = line.endColor = new Color(1f, 0.75f, 0.1f, 0.85f);
        line.startWidth = line.endWidth = 0.12f;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.sortingOrder = 5;
        line.positionCount = 0;
    }

    private void Update()
    {
        var pointer = Pointer.current;
        if (pointer == null) return;

        if (!InputEnabled || board.Busy)
        {
            if (dragging) CancelChain();
            return;
        }

        bool pressed = pointer.press.isPressed;
        Vector3 worldPos = cam.ScreenToWorldPoint(pointer.position.ReadValue());
        worldPos.z = 0f;

        if (pressed)
        {
            dragging = true;
            var tile = board.TileAt(worldPos);
            if (tile != null) TryAddOrBacktrack(tile);
            UpdateLine(worldPos);
        }
        else if (dragging)
        {
            dragging = false;
            SubmitChain();
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
        if (chain.Count >= 2 && tile == chain[^2])
        {
            chain[^1].SetSelected(false);
            chain.RemoveAt(chain.Count - 1);
            NotifyWordChanged();
            return;
        }

        if (chain.Contains(tile)) return; // each tile only once
        if (!Board.AreAdjacent(chain[^1].Cell, tile.Cell)) return;

        AddTile(tile);
    }

    private void AddTile(Tile tile)
    {
        chain.Add(tile);
        tile.SetSelected(true);
        NotifyWordChanged();
    }

    private void NotifyWordChanged() => game.OnChainChanged(CurrentWord());

    private string CurrentWord()
    {
        var chars = new char[chain.Count];
        for (int i = 0; i < chain.Count; i++) chars[i] = chain[i].Letter;
        return new string(chars);
    }

    private void UpdateLine(Vector3 pointerWorld)
    {
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

    private void SubmitChain()
    {
        game.SubmitChain(new List<Tile>(chain), CurrentWord());
        ClearChain();
    }

    public void CancelChain()
    {
        dragging = false;
        ClearChain();
        game.OnChainChanged("");
    }

    private void ClearChain()
    {
        foreach (var tile in chain)
            if (tile != null) tile.SetSelected(false);
        chain.Clear();
        line.positionCount = 0;
    }
}
