# Wordrift — architecture

Drag across adjacent letter tiles to spell words. Valid words demolish; tiles fall in.

## The one rule

**`GameSession` runs the loop. `GameMode` decides the rules. `RunState` is the
run.** The session never knows what a timer is; if you're about to add
`if (mode == RogueDemo)` anywhere, add a mode instead. And nothing ever writes
into an authored asset at runtime: configs and letter sets are read-only
recipes, and everything a run changes (its sack of tiles, its round number —
later money and bookmarks) lives on `RunState.Current`, a plain C# static that
survives scene loads the same way `ModeSelection` does.

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
| `Scripts/Core` | Board, Tile, TileSpec, ChainController, ScoreCalculator, WordValidator, gravity, modifiers | nothing above it |
| `Scripts/Modes` | `GameMode` + one class per mode | Core, Config, GameSession, RunState |
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
the asset, add a menu button pointing at it. Nothing existing changes. The mode
gets the session in `Attach`, so shared state the session owns (the score) is
read from there, never duplicated. A mode whose round flows somewhere other
than the game-over panel calls `session.ContinueTo(scene)` from its `End()`
override — that skips the panel (RogueDemo's cleared round goes to the shop
this way).

**A new HUD element** — a MonoBehaviour that subscribes to a `GameEvents` event
in `OnEnable` and unsubscribes in `OnDisable`. Drop it on the HUD Canvas. The
session doesn't need to know it exists.

**A special tile** — for another multiplier, just duplicate one of the four
assets in `Assets/GameData/Modifiers/` and change its `multiplier`, `badgeLabel`
and `badgeColor`; no code. For a genuinely new *rule*, subclass `TileModifier`
and override `ModifyLetterScore` or `WordMultiplier`. Either way, add it to a
mode config's `tileModifiers` — the pool of upgrades that mode can hand out.
A modifier reaches an actual tile only via `TileSpec.AddModifier` (an upgrade
on a specific tile in the run's sack); nothing spawns with one randomly.

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

**A mode with a finite supply of tiles** — install an `ITileSource` on
`Board.TileSource` in `GameMode.Attach`. `TileSack` drains a copy of a stock
list and draws without replacement; when it empties, `Board.SpawnTile` returns
null and cells it would have filled stay empty. The board resets the source
before every full fill, and `Reset` re-copies from the stock — so a replay (and
every round of a run) starts on a full sack, and tiles a shop added to the
stock are simply in it. `RogueDemoMode` is the worked example.

**The run.** `RunState.StartNew(config)` builds the sack from the `LetterSet`'s
weights read as tile counts (`LetterSet_Scrabble` sums to Scrabble's 98) and
holds it as `List<TileSpec>`. A `TileSpec` is a tile's persistent identity —
`letters` (a *string*, because multi-letter tiles like "qu" are planned even
though everything downstream still plays one char), `baseScore` (stamped from
the `LetterSet` catalog by `CreateSpec`, the one place specs are born — so one
tile's worth can diverge from its letter's), plus baked-on modifiers (a bought
2L tile keeps its 2L, and one tile can stack several — `TileSpec.AddModifier`
is how a shop applies an upgrade).
The flow: menu always ends any stale run; `RogueDemoMode.Attach` finds
`RunState.Current` or starts one; a cleared round continues to the Shop scene
(a stub — `ShopScreen` shows what cleared and what's next, its Continue
advances the round and reloads Game; it also gilds three random sack tiles
with random modifiers on every visit, marked TEMPORARY in the code, purely so
upgrades can be seen working before there's anything to buy); a failed round
ends the run, so the panel's PLAY AGAIN starts a fresh one at round 1. Round targets come from
`RogueDemoModeConfig.roundTargets` (authored per round, `targetGrowth` compounds
past the end of the list).

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
- **Stacked-modifier visuals.** A tile draws one badge per modifier, fanned
  10% of a badge to the right and over the previous — a first pass so stacks
  are visible, not the final treatment.
- **Wild tiles.** Not designed yet: a special `TileSpec.letters` value ("?") or
  a `TileModifier` are both plausible. Decide before building.
- **The HUD's one spare slot.** Round, target and sack share `ModeStatus.Goal`,
  one shrunken string. A run HUD (round, money, target, sack) needs a real
  multi-readout `ModeStatus` and widget.
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
`Create Tile Modifier Assets`, `Create Rogue Demo Mode Asset`, `Create Shop
Scene` (never touches an existing Shop.unity), `Set Up Board Background`, and
`Repair Scene References`. They only fill in what's missing, so
hand tuning survives — CLAUDE.md has the per-item detail on what each one will
and won't overwrite.
