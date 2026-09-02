# Wordrift — architecture

Drag across adjacent letter tiles to spell words. Valid words demolish; tiles fall in.

## The one rule

**`GameSession` runs the loop. `GameMode` decides the rules. `RunState` is the
run.** The session never knows what a timer is; if you're about to add
`if (mode == RogueDemo)` anywhere, add a mode instead. And nothing ever writes
into an authored asset at runtime: configs and letter sets are read-only
recipes, and everything a run changes (its bag of tiles, its round number, its
money — later bookmarks) lives on `RunState.Current`, a plain C# static that
survives scene loads the same way `ModeSelection` does.

```
tap / drag  ->  ChainController  ->  a SELECTION, and nothing more
                                          |
ENTER / DISCARD  ->  GameSession  <-------+
                                  ->  WordValidator   (is it a word?)
                                  ->  ScoreCalculator (how many points?)
                                  ->  GameMode        (spend a resource? round over?)
                                  ->  Board           (demolish + refill)
                                  ->  GameEvents      (tell the HUD)
```

**Choosing tiles and committing to them are separate steps.** `ChainController` reports a
selection and never submits — lifting the pointer does nothing. `GameSession.SubmitSelection`
and `GameSession.DiscardSelection` are the two ways out of a selection, both driven by the
HUD's buttons (`WordActionsWidget`). The gap between the two is what makes discarding
possible at all, so don't collapse it back into a submit-on-release.

Everything a widget needs to draw those buttons arrives in one `SelectionState`, published by
the session: the word, the tile count, the two *decisions* `CanSubmit` / `CanDiscard`, and the
live `ScorePair` preview. Widgets obey those rather than re-deriving them, which is what keeps
a button from offering something the session would refuse — and a preview from disagreeing
with the score.

**Scoring is two numbers.** `ScoreCalculator.Base` gives `Points × Mult` — tiles (through their
own 2L/3L and 2W/3W) times a multiplier from word length. That pair is what the HUD shows live.
`Evaluate` runs the run's bookmarks over it in slot order, each recording a `ScoreStep`, and the
HUD replays those steps one beat at a time after ENTER. A bookmark can add points, add mult, or
multiply mult; the additive and multiplicative forms don't commute, which is what makes the
order bookmarks sit in a real decision.

## Layout

| Folder | Contains | Depends on |
|---|---|---|
| `Scripts/Core` | Board, Tile, TileSpec, ChainController, ScoreCalculator, WordValidator, gravity, modifiers | nothing above it |
| `Scripts/Modes` | `GameMode` + one class per mode | Core, Config, GameSession, RunState |
| `Scripts/Config` | ScriptableObjects: LetterSet, board shapes, mode configs | Core |
| `Scripts/UI` | HUD widgets, each listening to `GameEvents` | Core |
| `Scripts/Save` | `RunSaveData` (the file's shape) and `RunSave` (the only code that touches disk) | nothing |
| `Scripts/Librarians` | `Librarian` + one class per rule a boss round can warp | Core |
| `Editor/` | Scaffold scripts; `WordCrushSetup.cs` regenerates assets/prefabs/scene | everything |

Core never references Modes or UI. That's what keeps modes cheap to add.
`Scripts/Save` references nothing at all — it's DTOs and a file — so the layer that
depends on it (`RunState`, `GameSession`, `GameMode`, `ShopScreen`) is the layer that
already knows what a run is.

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

**Anything a run or a round remembers** — put it in `RunState` (run-scoped) or on
the `GameMode` (round-scoped), and then **capture it**, or it silently resets the
next time the player continues. Run-level state goes in `RunState.Capture` /
`RunState.Resume`; a mode's own resources go in `GameMode.CaptureRound` /
`RestoreRound`, which `RogueDemoMode` implements for its moves, discards and bag.
The failure mode is quiet — a resumed run with one counter reset still looks like
a working run — which is why it's a rule and not a reminder.

Two things make the saving work, and neither is obvious:

- **A tile's identity is its index in `RunState.TileBag`.** The bag is serialized once
  by value; the board and the drawn-down bag are lists of indices into it. That's how a
  shop upgrade bought before a save still lands on the same tile after one.
- **The save is only ever written with the board at rest.** Clearing a word leaves holes
  in the columns for `settleDelay`, and a snapshot taken there restores a board with
  permanent gaps. `GameSession` therefore queues a save and flushes it from `Update`.

**A librarian (a boss round)** — subclass `Librarian`, override `PowerText` (its description,
derived from its own fields so it can't go stale) and whichever of the two hooks it needs:
`Apply(RoundRules)` to change the round's allowances before it starts, `Refuse(WordCheck)` to
rule words out while the player is choosing. Create the asset and add it to a mode config's
`librarians`; the run does the rest — `RunState.PickLibrarian` decides which round gets one and
draws so that none repeats until all have been seen.

Two rules that aren't obvious:

- **A librarian is a recipe, not a thing with state**, exactly like a `Bookmark`. Anything it
  needs to know about the round in progress arrives in the `WordCheck` — which is also why
  `DistinctLengthLibrarian` needs no save support at all: it reads the words already played,
  and those are in the snapshot already.
- **Widen `RoundRules` or `WordCheck`; don't add a hook.** Two moments cover a round, and the
  next lever a librarian wants (the score target, the refill policy, a turn at the
  `ScoringContext`) is a field on a bundle that already gets passed, not a third signature for
  every existing librarian to ignore.

**A new HUD element** — a MonoBehaviour that subscribes to a `GameEvents` event
in `OnEnable` and unsubscribes in `OnDisable`. Drop it on the HUD Canvas. The
session doesn't need to know it exists.

**A special tile** — for another multiplier, just duplicate one of the four
assets in `Assets/GameData/Modifiers/` and change its `multiplier`, `badgeLabel`
and `badgeColor`; no code. For a genuinely new *rule*, subclass `TileModifier`
and override `ModifyLetterScore` or `WordMultiplier`. Either way, add it to a
mode config's `tileModifiers` — the pool of upgrades that mode can hand out.
A modifier reaches an actual tile only via `TileSpec.AddModifier` (an upgrade
on a specific tile in the run's bag); nothing spawns with one randomly.

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
`Board.TileSource` in `GameMode.Attach`. `TileBag` drains a copy of a stock
list and draws without replacement; when it empties, `Board.SpawnTile` returns
null and cells it would have filled stay empty. The board resets the source
before every full fill, and `Reset` re-copies from the stock — so a replay (and
every round of a run) starts on a full bag, and tiles a shop added to the
stock are simply in it. `RogueDemoMode` is the worked example.

**Randomness.** Every roll in a run comes from `Rng` — SplitMix64 written out in our own code,
because `UnityEngine.Random` is a global any code can perturb and `System.Random` isn't stable
across .NET runtimes. `RunState` holds an 8-character `SeedCode` and vends independent streams
by name and round (`StreamFor(RunState.BagStream)`). Independence is the point: a change to how
often one system rolls can't shift another's draws, so recorded seeds survive code changes.
Core stays run-ignorant — `TileBag` is handed its stream in `GameMode.Attach`, like every other
policy.

**The run.** `RunState.StartNew(config)` builds the bag with
`LetterSet.BuildTileBag(config.tileBagSize)` and holds it as `List<TileSpec>`.
The split there is load-bearing: the `LetterSet`'s weights are a **ratio**, the
config's `tileBagSize` is the **count**, and `BuildTileBag` shares the one out
over the other by largest remainder with a floor of one of every letter. So the
bag can be resized — by a config tweak now, by an upgrade later — without
re-authoring 26 weights, and at 98 it still reproduces Scrabble exactly. A `TileSpec` is a tile's persistent identity —
`letters` (a *string*, because multi-letter tiles like "qu" are planned even
though everything downstream still plays one char), `baseScore` (stamped from
the `LetterSet` catalog by `CreateSpec`, the one place specs are born — so one
tile's worth can diverge from its letter's), plus baked-on modifiers (a bought
2L tile keeps its 2L, and one tile can stack several — `TileSpec.AddModifier`
is how a shop applies an upgrade).
The flow: menu always ends any stale run; `RogueDemoMode.Attach` finds
`RunState.Current` or starts one; a cleared round pays out and continues to the
Shop scene (`ShopScreen` shows what cleared, what it paid, and what's for sale;
its Continue advances the round and reloads Game); a failed round ends the run,
so the panel's PLAY AGAIN starts a fresh one at round 1. Round targets come from
`RogueDemoModeConfig.roundTargets` (authored per round, `targetGrowth` compounds
past the end of the list).

**Bookmarks.** The run's items, and the answer to `ROGUELIKE-IDEAS.md`'s "relics need to
intercept scoring". `ScoreCalculator` now ends with an open stage: it builds a
`ScoringContext { Word, Tiles, WordsThisRound, Points, Mult }` and hands it to each of the
run's bookmarks **in slot order**, then multiplies. A `Bookmark` is an authored
ScriptableObject with one method (`OnWordScored`); a `BookmarkSpec` is the copy a run owns,
and it exists so editions can live on the owned copy rather than the shared asset — the same
recipe/instance split as `TileModifier` → `TileSpec`. The session gets the list from
`GameMode.Bookmarks` (null for a mode with no run), so `Scripts/Core` still knows nothing
about runs. To add a bookmark: subclass `Bookmark`, create the asset, add it to a mode's
`bookmarks` pool. To add a bookmark that needs new information, widen `ScoringContext`.

**Money.** `RunState` owns the balance: in through `AddMoney` only, out through
`TrySpend` only (it refuses rather than going negative), and gone when the run
is — so there's nothing to persist and no meta-currency to design around. What a
cleared round pays is `RogueDemoModeConfig.RewardFor(score, movesLeft)`: one
method on the authored asset, called from `RogueDemoMode.End` before the scene
change, and the seam for interest or payout bookmarks later. `GameSession`,
`GameEvents` and everything in Core stay ignorant of currency — the money
readout rides the `ModeStatus.Goal` string the mode already fills in.
The shop's *stock* is temporary and banner-marked in the code; the purchase
plumbing (price on a `TileModifier`, `TrySpend`, `TileSpec.AddModifier`) is not.
The game's rules and numbers, including which of them are placeholder, live in
`Wordrift_Encyclopedia.md` — keep it current.

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
- **The HUD's one spare slot.** Round, target, bag AND money now share
  `ModeStatus.Goal`, one shrunken string four readouts wide — close to
  overflowing the 400px name label. A run HUD needs a real multi-readout
  `ModeStatus` and widget; wiring `StatusWidget.goalLabel` to a dedicated label
  is the cheap interim fix.
- **Price lives on `TileModifier`.** Fine while the shop only sells modifiers;
  once it stocks bookmarks, tiles and bag upgrades, price belongs on an *offer*
  asset rather than on a modifier's identity.
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
