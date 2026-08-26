# Wordrift — architecture

Drag across adjacent letter tiles to spell words. Valid words demolish; tiles fall in.

## The one rule

**`GameSession` runs the loop. `GameMode` decides the rules.** The session never
knows what a timer is. If you're about to add `if (mode == Timed)` anywhere,
add a mode instead.

```
drag  ->  ChainController  ->  GameSession  ->  WordValidator   (is it a word?)
                                            ->  ScoreCalculator (how many points?)
                                            ->  GameMode        (spend a resource? round over?)
                                            ->  Board           (demolish + refill)
                                            ->  GameEvents      (tell the HUD)
```

## Layout

| Folder | Contains | Depends on |
|---|---|---|
| `Scripts/Core` | Board, Tile, ChainController, ScoreCalculator, WordValidator, gravity, modifiers | nothing above it |
| `Scripts/Modes` | `GameMode` + one class per mode | Core, Config |
| `Scripts/Config` | ScriptableObjects: LetterSet, board shapes, mode configs | Core |
| `Scripts/UI` | HUD widgets, each listening to `GameEvents` | Core |
| `Editor/WordCrushSetup.cs` | Regenerates assets/prefabs/scene from code | everything |

Core never references Modes or UI. That's what keeps modes cheap to add.

## Where things live

- **Numbers** (round length, move count, min word length, letter values, spawn
  weights, board size) live in assets under `Assets/GameData/`, not in code.
- **Visuals** (tile colors, fall speed, animation timings, fonts, layout) live
  on the prefabs under `Assets/Prefabs/`.
- **Art** lives in the `LetterSet` asset — one sprite slot per letter. Nothing
  looks up sprites by filename at runtime.

## Adding things

**A new mode** — subclass `GameMode` (rules) and `ModeConfig` (numbers), create
the asset, add a menu button pointing at it. Nothing existing changes.

**A new HUD element** — a MonoBehaviour that subscribes to a `GameEvents` event
in `OnEnable` and unsubscribes in `OnDisable`. Drop it on the HUD Canvas. The
session doesn't need to know it exists.

**A special tile** — for another multiplier, just duplicate one of the four
assets in `Assets/GameData/Modifiers/` and change its `multiplier`, `badgeLabel`
and `badgeColor`; no code. For a genuinely new *rule*, subclass `TileModifier`
and override `ModifyLetterScore` or `WordMultiplier`. Either way, set
`spawnChance` and add it to a mode config's list.

**A new tile look** — create a `TileSkin` (body sprite + letter/score colors +
spawn weight) and add it to a mode config's `Tile Skins`. Several in one list
means tiles draw a random skin each, so looks can be mixed on one board. The
letter's typeface is a separate axis (`letterFont`) so the two never entangle.

**A new board shape** — subclass `BoardShapeAsset` and return any set of cells.
The board, gravity and the board background all work over arbitrary cell sets;
nothing assumes rectangles. `BoardBackground` draws one square per cell and lets
their overlap form the border, so an odd silhouette needs no extra art.

**A mode that manages the board itself** — override `GameMode.Attach` to swap
`Board.Refill` (`IRefillPolicy`) or `Board.Gravity`, then drive the board from
`Tick`. `Attach` runs before `Board.Build`, which is the only window in which
those policies still matter for the opening fill. `OverflowMode` is the worked
example: `NeverRefill` plus its own drop clock.

**A different word list** — swap the TextAsset on `GameSession`. Plain text, one
lowercase word per line.

## Known open questions

- **Gravity through holes.** `ColumnGravity` drops tiles straight down to the
  lowest empty cell in their column. On a shaped board with holes, tiles fall
  *past* the holes. Swap `Board.Gravity` for another `IGravityRule` when we
  decide what should actually happen.
- **`GameEvents` is static.** Fine for one session at a time; it's what makes
  HUD prefabs drop-in with no wiring. Would need revisiting for split-screen or
  simultaneous boards.
- **One modifier per tile.** `Board.RollModifiers` stops at the first hit.
- **Overflow gets easier as it gets more dangerous.** A fuller board means more
  letters and more adjacency, so words are *easier* to find right when you're
  closest to losing. Whether that self-correcting equilibrium is fun or just
  makes the mode hard to lose is a playtest question, not a code one.

## Regenerating

`Word Crush -> Rebuild Game Scene & Assets` recreates `Assets/GameData`,
`Assets/Prefabs`, and `Assets/Scenes/Game.unity`. It's a starting point, not
something to keep re-running — hand edits to those assets are the normal path,
and rebuilding overwrites the scene.

`Word Crush -> Set Up Tile Prefab` re-authors the tile's letter and score
labels. `Word Crush -> Create Tile Skin Asset` creates the default skin and adds
it to every mode config. Both are idempotent and touch no scene.
