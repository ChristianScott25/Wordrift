# Wordrift — architecture

Drag across adjacent letter tiles to spell words. Valid words demolish; tiles fall in.

## The one rule

**`GameSession` runs the loop. `GameMode` decides the rules.** The session never
knows what a timer is. If you're about to add `if (mode == RogueDemo)`
anywhere, add a mode instead.

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
| `Editor/` | Six scaffold scripts; `WordCrushSetup.cs` regenerates assets/prefabs/scene | everything |

Core never references Modes or UI. That's what keeps modes cheap to add.

## Where things live

- **Numbers** (round length, move count, min word length, letter values, spawn
  weights, board size) live in assets under `Assets/GameData/`, not in code.
- **Visuals** (tile colors, fall speed, animation timings, layout) live on the
  prefabs under `Assets/Prefabs/`.
- **Art** is one shared body sprite per `TileSkin`, with the letter drawn over it
  as text. `LetterSet` holds no art at all — only letters, points and weights —
  so the look of a tile and the rules of the alphabet vary independently. The
  typeface is a third axis (`letterFont` on the mode config). All current
  art is placeholder, and nothing looks up sprites by filename at runtime.

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
those policies still matter for the opening fill. Overflow mode was the worked
example (`NeverRefill` plus its own drop clock) until it was cut; the pieces it
used are still in Board, unused.

**A mode with a finite supply of letters** — install an `ILetterSource` on
`Board.Letters` in `GameMode.Attach`. `TileBag` reads a `LetterSet`'s weights as
tile counts and draws without replacement; when it empties, `Board.SpawnTile`
returns null and cells it would have filled stay empty. The board resets the
source before every full fill, so a replay starts on a full bag for free.
`RogueDemoMode` is the worked example.

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
- **An unplayable board that isn't empty.** A finite-bag mode ends the round when
  the bag is dry and fewer tiles remain than `minWordLength` — provably nothing
  to play. But a board can hold ten tiles that spell nothing, and moves only tick
  down on a submitted word, so the round stalls. Options are a "no moves left"
  detector (expensive: it's a dictionary search over every path), a discard/pass
  button that costs a move, or a shuffle. Not decided.

## Regenerating

`Word Crush -> Rebuild Game Scene & Assets` recreates `Assets/GameData`,
`Assets/Prefabs`, and `Assets/Scenes/Game.unity`. It's a starting point, not
something to keep re-running — hand edits to those assets are the normal path,
and rebuilding overwrites the scene.

Every *other* item on that menu is idempotent and safe to re-run: `Set Up Tile
Prefab` (re-authors the tile's letter and score labels), `Create Tile Skin Asset`,
`Create Tile Modifier Assets`, `Create Rogue Demo Mode Asset`, `Set Up Board
Background`, and `Repair Scene References`. They only fill in what's missing, so
hand tuning survives — CLAUDE.md has the per-item detail on what each one will
and won't overwrite.
