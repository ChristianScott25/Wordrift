# Wordrift — roguelike idea space

**Nothing here is decided.** This is a menu of options from one brainstorming
conversation on 2026-08-25, written down so a new session starts with context instead of a
blank page. Christian has *not* committed to any of it — not the scope, not the mechanics,
not the order. Do not treat this as a backlog, do not start implementing off it, and do not
tell him "the plan says". Ask him what he actually wants to try.

The premise: Wordrift is a fork of Word Crush taken at `52a10a7`, made to explore whether
the game works as a **Balatro-style roguelike** — escalating score targets, a build you
assemble over a run, permanent modifications to your letter pool, and rule-warping boss
rounds. Word Crush itself stays the straightforward arcade version.

---

## Where the fork starts

Everything in `ARCHITECTURE.md` and `CLAUDE.md` still describes this project accurately —
read those first, they are the real documentation. Short version of what already exists:

- Three modes, all subclasses of `GameMode` + `ModeConfig`: **Timed** (clock, bonus seconds
  for long words), **Moves** (fixed word count), **Overflow** (Tetris-like, tiles drip in,
  board filling up ends the round).
- Scoring runs through `ScoreCalculator.Evaluate`: base letter values → per-tile
  `TileModifier`s (2L/3L) → word multipliers (2W/3W) → length bonus → the mode's
  `scoreMultiplier`.
- Tiles are one shared sprite with world-space TMP text on top. Looks come from `TileSkin`
  assets; `TileLook { Skin, LetterFont }` keeps art and typeface independent.
- Board population is already policy-driven: `IGravityRule`, `IRefillPolicy`, `IBoardShape`,
  installed in `GameMode.Attach` which runs *before* `Board.Build`.
- `GameEvents` is the only channel from gameplay to UI.

**No roguelike code exists yet.** Not a line.

---

## Mappings that already fit the codebase

These are the ideas that landed well, roughly in order of how cleanly they'd slot in.

### The letter bag is the deck

`LetterSet` already holds letters, point values, and spawn weights — exactly the three axes
a run would want to modify. "Add three more E's", "remove every Q", "S is worth 5 now",
"vowels spawn twice as often" are all edits to a bag carried between rounds. This is the
strongest single mapping and the cheapest to prototype.

**Partly built 2026-08-28.** The bag itself is real and carried between rounds
(`RunState.TileBag`), and its SIZE is now an authored number (`tileBagSize`, currently 52)
that `LetterSet.BuildTileBag` shares the catalog's weights out over — so "a bigger bag" is
already one field away. Editing the bag's *contents* is still only what the shop's tile
upgrades do; adding, removing or re-valuing letters mid-run doesn't exist.

### Boss rounds are policy swaps

Balatro's boss blinds warp one rule for one round. `IGravityRule` / `IRefillPolicy` /
`IBoardShape` are already swappable, and `GameMode.Attach` is already the window where
swapping them still affects the opening fill. "The board is a diamond this round", "nothing
refills", "tiles fall sideways" cost almost nothing structurally.

**Discussed 2026-08-31, DEFERRED by Christian — "sounds like a big change, record it but save
it for later."** Not started; the notes below are the state of the conversation, not a plan.

This came up as *the run has no ending* — the biggest structural hole left. Today you play
until a target beats you, so **every run ends in failure**, and nothing built during a run has
a destination. It also blocks tuning: targets are `30 / 45 / 65` then ×1.5 forever, so "is
round 6 too hard?" has no answer while there's no round 8 to aim at.

The questions that need his call before anything is built:

- **How long is a run, and what shape?** A flat list of N rounds with every third a boss, or
  Balatro's antes-as-groups where the boss pays more and the shop follows it? This decides how
  much economy a run has — at roughly 7 shop visits you can afford 8–12 purchases, about one
  coherent build.
- **What does a boss round DO?** The interesting answers are rule twists, not bigger numbers:
  a board with holes, no refill so you play the board down, a bag with no vowels, a minimum
  word length of 5. Which of those sound *fun* matters more than any of the engineering.
- **What happens when you win?** Stop on a victory screen, or continue into endless with the
  targets compounding, as Balatro does?

Two findings that surfaced in the same conversation and belong with it:

- **The payout is dominated by unused moves.** Clearing round 1 with 16 of 20 moves left pays
  ~$3 from score and **$16 from unused moves** — so the game pays you to clear *fast* and pays
  you almost nothing for a big word. That's backwards for a game built around watching a score
  cascade. It's a `RogueDemoModeConfig.RewardFor` change, but it's entangled with run length,
  so it waits for this.
- **Bookmark order now changes the score and the player can't reorder them.** Since scoring
  became Points × Mult (2026-08-31), a `+4 Mult` bought before a `×2 Mult` is worth far more
  than the reverse — and the shop decides the order. Small to fix, genuinely a gap.

### Word length tiers are hand types

Pair / Flush / Full House → 3-letter / 4-letter / 5-letter / 6+. Balatro's Planet cards
become something that permanently levels a tier for the rest of a run, which forces the
build-defining choice: go wide on short words, or tall on long ones.

### Tile modifiers are card enhancements

`TileModifier` already does per-letter and per-word multiplication with a badge. Balatro
layers three independent things on a card (enhancement + edition + seal); this currently
supports **one modifier per tile** — `Board.RollModifiers` stops at the first hit — so
multi-slot tiles would be a real change, not a config tweak.

---

## The part that needs actual work: relics / jokers

This is where Balatro lives, and it's the piece the current architecture is *not* shaped
for. `ScoreCalculator.Evaluate` is a closed function with a hardcoded order. Relics need to
**intercept** that sequence, and they need hooks well beyond scoring — round start, per tile
scored, on a rejected word, on refill, on money earned.

The shape that was discussed: turn `Evaluate` into an ordered pipeline over a mutable
`ScoringContext { Points, Mult, ... }` that a list of relics gets to touch **in slot order**,
so owning the same three relics in a different order gives a different result. That ordering
is a large chunk of where Balatro's depth comes from.

This is a refactor of a core file, not a bolt-on. It should be a deliberate decision, not
something that happens on the way to something else.

**Built 2026-08-27.** The pipeline above now exists: `ScoringContext { Points, Mult }`,
mutated by the run's bookmarks in slot order, as the final stage of `ScoreCalculator`.
Three bookmarks use it — Bookend (from the table below), Deja Vu, Vowel Fanatic — and the
shop sells one per visit. The hooks beyond scoring (round start, per tile, on refill, on
money earned) do **not** exist; only the word-scored one does.

Relic ideas, only to show the space is wide — not a shortlist:

| Name | Effect |
|---|---|
| Lexicographer | +15 mult if the word's letters are in alphabetical order |
| Bookend | ×2 if the word starts and ends with the same letter |
| Hoarder | +2 mult per unused vowel left on the board |
| Scrabbler | Q, X, Z, J score triple |
| Chain Reaction | each word this round scores +5 mult more than the last |
| Demolitionist | clearing a bottom-row tile pays +1 money |
| Understudy | 3-letter words score as though they were 5 |

---

## Open questions — his call, not yours

**1. Timer or turns?** The biggest one. Balatro's tension comes from a *small number of
deliberate plays*; Word Crush is currently a frenzy against a clock. A build you spent three
shops assembling wants a moment to be admired, and a timer never gives one.

*Leaning discussed (not chosen):* the roguelike mode is **N words per round against a score
target**. It slots straight into `GameMode` — `OnWordAccepted` decrements, `IsRoundOver`
checks the count, and `ModeStatus` already renders "WORDS 3" with no new HUD work. Timed and
Overflow stay untouched.

**2. What's the discard analog?** Balatro's discards let you dig toward a hand. Here the
board *is* the hand, so the analog is board manipulation — limited shuffles, "bomb a tile",
"reroll a column". *Leaning:* whole-board shuffle, N per round, purchasable upward.

**3. How far does this go?** There's a version that's a **mode** (score targets, escalating
antes, a handful of relics from a shop) and a version that's the **whole game** (money with
interest, consumables, booster packs, a voucher tree, 150 relics, meta-unlocks). The first is
a modest amount of work on top of what exists. The second is a different project. He has not
picked.

---

## Known traps if any of this gets built

- **Run state cannot live in ScriptableObjects.** Mutating a `LetterSet` asset at runtime
  permanently rewrites the asset on disk in the editor — you'd finish a playtest and find
  your letter weights are whatever the last run left them at. A run needs a `RunState`
  holding *clones*: modified tile bag, owned relics, money, ante, tier levels.
- **Scene handoff.** Object references don't survive a scene load. The existing static
  `ModeSelection.Take()` pattern is the right thing to extend for carrying a run into a shop
  scene and back.
- **One modifier per tile today.** `Board.RollModifiers` stops at the first hit, and it walks
  the mode's list in order, so a modifier's real spawn rate is its chance times the chance
  every earlier one missed. Multi-slot tiles change both facts.
- **`ScoreCalculator` is the only place letter modifiers are applied**, and the tile's corner
  always shows the *base* letter value. Don't reintroduce a display-side multiply.
- **`Scripts/Core` must not reference `Scripts/Modes` or `Scripts/UI`.** Relics are rules, so
  they belong on the modes side of that line, not inside `Board` or `Tile`.

---

## Relationship to Word Crush

`../Word-Crush` is the same codebase at `52a10a7` and its own GitHub repo
(`ChristianScott25/Word-Crush`). Shared history means `git cherry-pick` moves a core fix
across cleanly — worth doing for genuine bug fixes in `Board`, `Tile`, `ChainController`.

Roguelike work does **not** belong upstream. The editor menu is deliberately still
`Word Crush/*` in both projects; renaming it here would conflict on every cherry-pick of an
editor script.
