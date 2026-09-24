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
| `Scripts/Checkouts` | `Checkout` + one class per permanent run-wide perk, and `RunPerks` | nothing |
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

**The UI changing the run** goes back the other way from `GameEvents`, which is
gameplay → UI only. Two shapes are in use and both are fine: a direct serialized
reference to the host (`WordActionsWidget.session`, for a widget that lives in one
scene), or `RunState.Changed` (for one that lives in several, like
`BookmarkRowWidget`, where the hosts differ — `GameSession` QUEUES a save because
the file may only be written with the board at rest, and `ShopScreen` writes at
once). Don't add UI-raised events to `GameEvents`; that's what keeps it readable
as one direction.

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
`Apply(RoundRules)` to change the round's allowances — and its board — before it starts,
`Refuse(WordCheck)` to rule words out while the player is choosing, `Score(ScoringContext)` to
change what a word is worth. Create the asset and add it to a mode config's
`librarians`; the run does the rest — `RunState.PickLibrarian` decides which round gets one and
draws so that none repeats until all have been seen.

Two rules that aren't obvious:

- **A librarian is a recipe, not a thing with state**, exactly like a `Bookmark`. Anything it
  needs to know about the round in progress arrives in the `WordCheck` — which is also why
  `LockedLengthLibrarian` needs no save support at all: it reads the words already played,
  and those are in the snapshot already.
- **Widen `RoundRules` or `WordCheck`; don't add a hook.** Three moments cover a round, and the
  next lever a librarian wants is a field on a bundle that already gets passed, not a fourth
  signature for every existing librarian to ignore. `RoundRules.TargetMultiplier` was the first
  of those — The Insatiable triples the round's target and cost one field; `ClosedCells` was the
  second, and gave The Dilapidated a board with holes in it. The obvious remaining lever is the
  board's refill policy.
- **`Apply` runs in `GameMode.Attach`, before `Board.Build`** — not in `Begin`. Closing cells is
  one of the levers, and the opening fill happens inside `Build`, so a librarian reshaping the
  board in `Begin` would be reshaping one already full of tiles. Anything `Apply` needs about the
  board therefore comes in on `RoundRules` (`BoardCells`), never off the `Board` itself.

**A checkout (a permanent run-wide perk)** — subclass `Checkout`, override
`PowerText` (derived from its own fields, like a librarian's) and
`Apply(RunPerks)`, create the asset, add it to a mode config's `checkouts`, and
add a block to `Assets/Editor/CheckoutSetup.cs`. `RunState` owns which ones a run
has bought and rebuilds `RunPerks` from that list; whoever needs the number reads
it off `run.Perks`.

Two rules that aren't obvious:

- **`Apply` must be idempotent and purely declarative.** `RunState` throws the
  whole `RunPerks` away and rebuilds it after every purchase and again on every
  resume, so `Apply` runs an unbounded number of times for one purchase. Describe
  a standing perk; anything that HAPPENS once — adding a tile to the bag, paying
  out money — belongs in the shop's `Deliver`, not here. This is the one way to
  write a checkout that is quietly, cumulatively wrong.
- **Widen `RunPerks`; don't add a hook.** Same bargain `RoundRules` takes. A perk
  is a field on a bundle that already gets passed, and each one should land in
  exactly one place: `ExtraMoves` and `ExtraDiscards` in
  `RogueDemoMode.BuildRules`, the money ones in `RewardFor`, the discount in
  `RunState.PriceOf`. Two call sites for one perk is two answers.

Note the ordering this buys for free: perks are folded into `RoundRules` *before*
the librarian sees them, so a librarian that lowers an allowance with `Mathf.Min`
beats a checkout that raised it — the boss beats the shop, without either one
knowing the other exists.

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

**A new kind of tile** — it's a CATALOG ROW, not a class. Add an `Entry` to the
`LetterSet` (`Assets/Editor/LetterSetSetup.cs` is the one place they're authored)
with what it spells, what it's worth and its spawn weight. A multi-letter entry
("ch") works the whole way through with no code: the lookup is keyed by the full
string, `LetterSet.CreateSpec` stamps it whole, `ChainController.WordOf`
concatenates each tile's spelling, and `ChainController.LetterCount` is what the
length multiplier reads. Weight 0 means "listed but never dealt" — which is how
the shop-only tiles stay out of the starting bag. `Entry.IsForSale` is what the
shop's tile row offers.

Three rows in and the shape is clear: **a tile's whole identity is its `letters`
string**, which is also the only thing saved about what it is. That's why a wild
is `"*"` and a choice tile is `"a/e/i"` rather than either being a flag or a
field — `RunState.Resume` rebuilds a spec from nothing but that string and a
score, so a new kind costs no save work at all.

The one rule to respect: **one tile is one character of the search pattern unless
it really does spell more.** `ChainController.WordOf`/`LetterCount`,
`Board.LetterCount` and `GameSession.ShowResolvedLetters` all walk a chain by
`tile.Letters.Length`. A choice tile therefore has three views on `TileSpec` —
`Face` ("a/e/i", what it draws and what the Censor scans), `Spelling` ("*", what
goes into the word) and `Options` ("aei", what the dictionary may try). Spelling
itself would have counted it as three letters toward the length multiplier and
kept a dead round alive in `Board.LetterCount`. A new undecided tile kind reuses
that split; a new *spelling* kind (a three-letter pair) needs none of it.

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
cleared round pays is `RogueDemoModeConfig.RewardFor(score, movesLeft,
payoutMultiplier, perks, moneyHeld)`: one method on the authored asset, called
from `RogueDemoMode.End` before the scene change, and the seam every payout idea
hangs off — interest and the payout bonus both landed there rather than in the
mode. `GameSession`, `GameEvents` and everything in Core stay ignorant of
currency — the money readout rides the `ModeStatus.Goal` string the mode already
fills in.
What a shop row COSTS goes through `RunState.PriceOf` and nowhere else, so the
price shown and the price charged cannot drift apart when a discount is in play.
The shop's *stock* is rolled in `ShopScreen` against five role-fixed slots; the
purchase plumbing (price on a `TileModifier` / `Bookmark` / `Checkout`,
`TrySpend`, `TileSpec.AddModifier`) is deliberately dull.
The game's rules and numbers, including which of them are placeholder, live in
`Wordrift_Encyclopedia.md` — keep it current.

**A different word list** — swap the TextAsset on `GameSession`. Plain text, one
lowercase word per line.

## Known open questions

- **Gravity through holes — answered, for one case.** `ColumnGravity` drops tiles
  straight down, so on a shaped board they fall *past* a hole to the lowest cell
  their column still has. The Dilapidated (a librarian that closes three cells)
  deliberately wants exactly that, so the behaviour is now load-bearing rather
  than merely undecided. It's still an open question for a permanently shaped
  board, where "tiles pour out of the bottom of a gap" may not be what's wanted;
  swap `Board.Gravity` for another `IGravityRule` there rather than changing this one.
- **`GameEvents` is static.** Fine for one session at a time; it's what makes
  HUD prefabs drop-in with no wiring. Would need revisiting for split-screen or
  simultaneous boards.
- **Stacked-modifier visuals.** A tile draws one badge per modifier, fanned
  right across the top of the tile at `Tile.badgeSpacing`, squeezed closer when
  they wouldn't otherwise fit inside it. Readable, and bounded by
  `ModeConfig.maxModifiersPerTile` — but three badges reach most of the way
  across a tile and sit over the letter, so it isn't the final treatment.
- **Wild tiles.** Not designed yet: a special `TileSpec.letters` value ("?") or
  a `TileModifier` are both plausible. Decide before building.
- **The HUD's one spare slot.** Round, target, bag AND money now share
  `ModeStatus.Goal`, one shrunken string four readouts wide — close to
  overflowing the 400px name label. A run HUD needs a real multi-readout
  `ModeStatus` and widget; wiring `StatusWidget.goalLabel` to a dedicated label
  is the cheap interim fix. `ModeStatus.Extra` is free again — it used to list
  the bookmarks, which are cards below the board now.
- **The screen is full, and the bookmark row is what proved it.** At 1080×1920
  the board is framed by WIDTH (`max(halfHeight, halfWidth / aspect)`, and the
  second term wins in portrait), so it takes 884 of 1920px and leaves 641 above
  / 395 below — against roughly 590px of top stack and, now, 460 of bottom. It
  fits on the 19.5:9 phones this is built for, where the board takes a smaller
  share of the height, and doesn't at 16:9. `BookmarkRowWidget` handles that by
  pinning itself to the board's real bottom edge and clamping at a floor, which
  is a workaround and not an answer: the next widget that wants a band will have
  to move something. `GameSession.verticalOffset` is the one knob, and it only
  trades room above for room below.
- **Price lives on the thing being sold** — `TileModifier.price`, `Bookmark.price`,
  `Checkout.price` — rather than on an *offer* asset. Three copies of the same
  field now, and the shop's `Offer.ListPrice` is the seam that hides it. It holds
  while everything sold is an asset the run can own; the day the shop sells
  something that isn't one (a reroll, a bag slot, a tile), price wants moving to
  an offer asset and `ListPrice` is where that change lands.
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
