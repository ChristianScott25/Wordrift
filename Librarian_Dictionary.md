# 📕 The Librarian Dictionary

> **Every librarian, in full.** `Wordrift_Encyclopedia.md` explains what a librarian *is* and
> how they fit into a run (§6); this is the per-character reference — what each one does, what
> its numbers are, how it actually plays, and the things about it that aren't obvious.
>
> **Keeping this current is a standing job.** A librarian added, retuned or cut is a change to
> this file in the same turn as the code — same rule as the Encyclopedia.

**Last updated:** 2026-10-22 · twelve librarians · the Librarian Lab
**Status:** the roster is a first pass. Every one of them is a *restriction* (see [Open](#open))

### How to read this

| Marker | Meaning |
|:--:|---|
| 🎯 | **Intent.** What the rule is *for* — the decision or feeling it's meant to create. |
| ⚠️ | **Watch out.** A known sharp edge, usually about a round that can't be finished. |
| ❓ | **Undecided.** A real design question left open. |
| 🚧 | **Temporary.** A placeholder value, not to be balanced around. |

Every number lives in that librarian's own asset in `Assets/GameData/Librarians/` and is
tunable in the Inspector. Each one **writes its own description from those numbers**
(`Librarian.PowerText`), so retuning a value can never leave the on-screen text describing the
old rule. `powerOverride` on any asset replaces the wording if you want to hand-write it.

### 🚧 Seeing one without playing for it — the Librarian Lab

**`Word Crush → Librarian Lab`.** A window listing every librarian in the project; **Play** next
to one forces it onto *every* round and drops you straight into the Game scene at round 1. Use it
to look at a boss, tune its numbers, and look again — instead of playing to round 3 and taking
whichever one the seed felt like.

**🚧 It is a test tool and it must never ship.** All of it lives in `Assets/Editor/LibrarianLab.cs`,
which Unity does not compile into a player, so it *cannot* reach a build. The only trace in game
code is `RunState.LibrarianOverride` and `RunState.RunStartedOverride` — static delegates that
are null in a build, because that editor file is the only thing anywhere that assigns them.
**Deleting the lab is that one file, those two fields, the five-line block in
`RunState.PickLibrarian`, and the one call in `RunState.StartNew`.** Nothing else knows it
exists, and normal play with nothing forced is byte-for-byte the game without it.

Five things it does that a real round doesn't:

- **A lab run starts with 2 random items** (different ones where the pool allows, capped by
  `maxConsumables`), so a librarian about items — The Shusher — has something to block. Only a
  FRESH run gets them (lab PLAY, or PLAY AGAIN after a loss); later rounds don't top them up,
  and nothing is given unless a librarian is being forced. Drawn from their own stream
  (`lab-items`), so they can't move anything a seed decides.
- ⚠️ **Playing from it overwrites the saved run.** It opens the Game scene directly, which starts
  a fresh run at round 1 and saves over `run.json`. There's a *Delete the saved run* button for
  when you'd rather clear it deliberately.
- ⚠️ **A librarian that isn't in the mode's `librarians` pool won't survive a CONTINUE.**
  `RunState.Resume` resolves the round's librarian by asset name against that pool and nothing
  else, so a brand-new one you haven't run `Create Librarian Assets` for yet resumes as an
  ordinary round. Testing it fresh works; resuming it doesn't.
- **Forcing bypasses the no-repeat pool entirely**, so `librariansUnseen` is untouched — a lab run
  can't disturb the rotation of a real one.
- **Pressing Play while the game is already running doesn't change the round in progress**, which
  was set up before you chose. It applies to the next one; the window says so in the console.

The choice is held in `SessionState`, which survives entering play mode (and the domain reload
that comes with it) but is forgotten when the editor closes — so a forced librarian can't quietly
outlive the session that asked for it. The status line at the top of the window always says what
is being forced, and **Stop forcing** puts rounds back to normal.

### The rules that apply to all of them

- **Every third round is a librarian round** *(`librarianEveryRounds`)*, and clearing one
  **pays double** *(`librarianPayoutMultiplier`)*.
- **Which one turns up is part of the run's seed**, and **none repeats until all twelve have been
  seen.** So rounds 3, 6, 9 … 36 are all different, and the cycle restarts at 39.
- **A word a librarian won't take can't be played at all.** It doesn't score zero and it doesn't
  cost a move — PLAY simply won't light up, and the librarian's reason is written in the
  selected word. Nothing is spent finding out.
- **A librarian is only ever asked about words the dictionary already accepted**, so a reason
  never ends up captioning a plain non-word.
- **Nothing warns you before the round begins.** You find out when you get there. ❓

### Contents

| | Librarian | In one line | Seam it uses |
|:--:|---|---|---|
| 1 | [The Grandiloquent](#1-the-grandiloquent) | Words must be 5+ letters | `Refuse` |
| 2 | [The Conformist](#2-the-conformist) | Every word the *same* length | `Refuse` |
| 3 | [The Abridged](#3-the-abridged) | No letter twice in one word | `Refuse` |
| 4 | [The Censor](#4-the-censor) | One letter banned, rolled per round | `Apply` + `Refuse` |
| 5 | [The Redactor](#5-the-redactor) | No discards | `Apply` |
| 6 | [The Insatiable](#6-the-insatiable) | Score target ×3 | `Apply` |
| 7 | [The Critic](#7-the-critic) | −25% Points and Mult | `Score` |
| 8 | [The Dilapidated](#8-the-dilapidated) | 3 board spaces are closed | `Apply` |
| 9 | [🚧 The Shusher](#9-the-shusher) | No items this round | `Apply` |
| 10 | [🚧 The Prefacer](#10-the-prefacer) | Words must start on a marked tile | `Apply` + `Refuse` |
| 11 | [🚧 The Epilogist](#11-the-epilogist) | Words must end on a marked tile | `Apply` + `Refuse` |
| 12 | [🚧 The Mildewed](#12-the-mildewed) | Infected tiles count against you | `Apply` + `Refuse` (+ Core scoring) |

---

## 1. The Grandiloquent

**Words must be 5 letters or longer.** *(`minimumLength`, default 5 — `Librarian_Grandiloquent.asset`)*

Refusal reads: *"Too short — 5 letters or longer."*

Your reliable three-letter filler is gone; every word has to be a real find. On a 5×5 board a
five-letter word is a genuine hunt, so this round is usually decided by two or three big words
rather than a steady drip.

**It does NOT raise `ModeConfig.minWordLength`**, and that's deliberate: that number is the zero
point of the length-multiplier curve, so moving it would silently re-price every word in the
game as well as banning the short ones. The Grandiloquent refuses words; it doesn't redefine
what a word is worth.

⚠️ The likeliest of all seven to deadlock a round — see [Open](#open).

---

## 2. The Conformist

**Your first word sets the length. Every word after it must be that same length.**

Refusal reads: *"Locked to 4-letter words this round."*

🎯 The whole round is decided by one choice made before you know what the board will give you.
Open with a three and it's threes all round; open with a six and you have promised to keep
finding sixes.

**Implementation note worth keeping.** It never records *which* length was chosen, because under
its own rule every word already played has that length — so any of them answers the question.
"Every word so far agrees" is the same rule as "match the first word", and it's order-free.
(`WordCheck.WordsThisRound` was a set with no order until 2026-10-29; it's a list in play order
now, so the first word *could* be read — but nothing needed changing.)

---

## 3. The Abridged

**No word may use the same letter twice.**

Refusal reads: *"No repeated letters — E twice."*

**Within one word only** — a letter refused here is completely free in your next word. So this
narrows every word without ever spending anything, which makes it the mildest of the word-rule
librarians.

🎯 It takes out the words you reach for without thinking: doubles (LETTER, BOOKS), and most
plurals where the S repeats a letter already used. The board reads differently rather than
merely worse.

The cheapest librarian there is — it doesn't even read the round, only the word in front of it.

---

## 4. The Censor

**One letter is banned for the round**, rolled when the round starts and **named on screen**:
*"The letter E may not be used."*

Refusal reads: *"E is banned this round."*

**⭐ The banned letter is weighted by what's in your bag.** It's drawn from a pool holding **one
entry per letter you own**, not one entry per distinct letter — so with a standard bag a banned
E (13 tiles) is thirteen times likelier than a banned Z (1 tile). A multi-letter tile puts in
every letter it spells, so a `CH` tile counts once toward C and once toward H, and a **choice
tile** puts in every letter it offers, so an `A/E/I` counts toward all three. A **wild** puts
in nothing: the pool takes `char.IsLetter` only, and a round that banned `*` would announce a
rule and then enforce nothing, since a resolved word never contains one. That is the entire point: a banned Z
is a shrug, and a boss round shouldn't be a shrug. It also means the letter tracks *your* bag —
if a run has been stacking vowels, the vowels are what's at risk.

**The letter is part of the seed, and it is never saved.** It's drawn from a stream keyed to the
round number, so quitting and resuming a Censor round re-derives exactly the same letter with
nothing written to disk. The draw happens once, in `Apply`, and nowhere else — drawing it in
`Refuse` would re-roll it on every keystroke.

**This is the only librarian that chooses anything**, and it's why `RoundRules` carries a random
stream, a letter pool, and a `Note` for the answer. Anything else that needs a per-round choice
uses the same three fields rather than inventing its own.

⚠️ It can strike out the letter the board happens to be full of, which is another route into the
dead-board problem — see [Open](#open).

⚠️ **A multi-letter tile puts every letter it spells into the pool, and a ban on either one
kills the whole tile.** A `CH` tile weights both C and H, and a round that bans C refuses every
word played through it — the refusal scans the letters of the word, and the word contains the
C whether you think of the tile as a C or not. That's the intended reading (the tile really does
spell a C), but it makes a bought tile fully dead for a round rather than merely awkward.

⚠️ **A CHOICE TILE SURVIVES A BAN ON ONE OF ITS LETTERS — the opposite of a `CH`.** An `A/E/I`
on an E-banned round is still an A or an I, because the resolver drops refused candidates before
it picks, exactly as it does for a wild. So a choice tile weights three letters in the pool and
is killed by none of them individually. A `CH` weights two and is killed by either. That
asymmetry is real and intended: a pair genuinely spells both letters, a choice tile spells one
you haven't picked yet.

⚠️ **A WILD BEATS THIS LIBRARIAN OUTRIGHT.** A wild becomes whichever letter scores best *among
the ones this round allows* — `GameSession.ResolveSelection` drops every refused candidate
before it picks — so with E banned, `B*D` quietly resolves to BAD and is never refused at all.
The Censor only lands on a wild when EVERY letter that fits the dictionary is banned, which for
one banned letter is close to never. This is the first thing in the game that can no-sell a
boss, it was asked for deliberately ("best option, boss wise"), and it is worth re-checking once
wilds have actually been played against a Censor round.

---

## 5. The Redactor

**No discards.** *(`discards`, default 0 — `Librarian_Redactor.asset`)*

Nothing changes about scoring or about which words are legal; the escape hatch is simply shut. A
bad board is yours to solve.

It's a **limit, never a grant** — it takes the minimum of your allowance and its own number, so
setting it to 3 on a round that only had 2 discards leaves you with 2.

⚠️ **It beats the Second Thoughts checkout**, and that's deliberate. Perks are folded into the
round's allowances *before* a librarian sees them, so a run that paid $25 for +2 discards every
round still gets none on this one. The boss beats the shop — which is what makes a boss worth
dreading and, ⚠️ note, what makes this the librarian most likely to feel like a refund is owed.

⚠️ Discards are the intended way out of a board that won't spell anything, so this is the
librarian that most directly removes the answer to the dead-board problem.

---

## 6. The Insatiable

**Score target ×3.** *(`targetMultiplier`, default 3 — `Librarian_Insatiable.asset`)*

The only librarian that changes nothing about *how* you play — every word you could have played,
you still can. It only moves the bar.

🎯 The clean test of whether a run's scoring is actually scaling. Restrictions ask whether you
can adapt; this asks whether you have built anything.

It multiplies a **factor**, never sets a number, so "three times whatever this round was going to
ask for" stays meaningful on round 3 and on round 30.

⚠️ **The shop's NEXT TARGET line doesn't know about it.** The next round's librarian isn't drawn
until you press CONTINUE, so the shop advertises the base target and the round then opens asking
for three times that. Known, undecided — see [Open](#open).

---

## 7. The Critic

**Every word loses 25% of its Points and its Mult.** *(`penalty` 0.25, `floor` 1 —
`Librarian_Critic.asset`)*

🎯 Like The Insatiable, it changes nothing about which words are legal — you play the round
exactly as you would have, and come up short. Unlike The Insatiable, it scales with what you've
built rather than against it.

Four things about how the cut is taken:

- **It runs after your bookmarks**, not before. It taxes what you built, so a run with strong
  bookmarks loses more in absolute terms and the same amount proportionally — which is what a
  percentage is for.
- **Points rounds UP.** A 10-point word keeps 8, not 7.
- **Mult is not rounded**, only floored. The length curve deals in 1.5s, and rounding the
  multiplier to whole numbers would quietly flatten the very curve this is meant to be taxing.
  ❓ Whether it should round is open.
- **Neither number goes below 1** *(`floor`)*, so a word is always worth something.

**It is not previewed.** The live POINTS × MULT readout shows the word untaxed; the cut lands as
its own named beat — **THE CRITIC −25%** — in the score walk-through after PLAY. That's the same
bargain bookmarks make, but ❓ it's a more questionable one for a *penalty*: you plan every word
against a number that is always 25% too high. See [Open](#open).

**The first librarian to use the scoring hook**, and the reason `Librarian.Score(ScoringContext)`
exists. Anything else that wants to change what a word is worth uses that same turn.

---

## 8. The Dilapidated

**Three spaces on the board are closed for the round**, drawn when the round starts.
*(`closedCells`, default 3 — `Librarian_Dilapidated.asset`)*

Nothing is refused and nothing is taxed. The board is just smaller, and a different shape:
22 spaces instead of 25, with three holes somewhere in the middle of them.

🎯 **The first librarian that changes the game instead of narrowing it.** Every other one is a
rule you have to remember while you play; this one is a board you have to read. The words you
can see are still legal — they're just not there any more.

Two things it does, and the second is the interesting one:

- **A closed space breaks adjacency.** Tiles either side of a hole are two apart, so they are
  not neighbours and a chain can't cross. Words route around, which on a 5×5 board is most of
  the difficulty: the long horizontal runs are the first thing to go.
- **Tiles fall THROUGH it.** When the space below a hole empties, the tiles above fall straight
  past the hole to fill it, and stop above the hole once the space below is full. The hole never
  fills. That isn't special-cased — a closed cell is subtracted from the board's shape, so its
  column simply has one fewer slot and the ordinary gravity compacts into what's left. It's the
  answer to the "do tiles fall through holes?" question `ColumnGravity` has been carrying a note
  about since the board was written.

**It chooses, like The Censor, and the choice is never saved.** The cells are drawn once, in
`Apply`, from the round-keyed stream — so quitting and resuming the round brings back the same
three holes with nothing written to disk. Where The Censor writes its letter into `RoundRules.Note`,
this fills in `RoundRules.ClosedCells`, which the mode hands to the board.

**It is the reason a librarian's `Apply` now runs in `GameMode.Attach` rather than in `Begin`.**
The opening fill happens inside `Board.Build`, so a librarian that reshaped the board in `Begin`
would be reshaping one already full of tiles. Everything else about `Apply` is unchanged, and the
round-keyed stream means the choices come out the same either way.

**The banner doesn't name the cells**, which is why there's no `PowerFor` override — unlike a
banned letter, the player can see these.

**The holes can't strand a space.** *(`minNeighbours`, default 2)* Left to a plain uniform draw,
`(1,0)`, `(0,1)` and `(1,1)` would wall `(0,0)` off completely and the tile sitting in it could
never be played or discarded. So a cell is only closed if **every space next to it keeps at least
two open neighbours** — which rules out 256 of the 2300 possible triples on a 5×5, edges as well
as corners.

Two things about how that's done:

- **The candidates are re-derived before every draw**, not filtered once, because closing one
  space changes which others are still safe to close.
- **The candidate list is built by walking the board's cells in order and testing membership**,
  never by iterating the open-cell set. A `HashSet`'s enumeration order isn't guaranteed stable
  across runtimes, so the second version would deal different holes in the editor and on device
  from the same seed — the exact class of bug `Rng` exists to prevent.

Adjacency is `Board.AreAdjacent`, the same call the chain uses, so "next to" means the same thing
to the rule as it does to the player's finger. ⚠️ It counts diagonals; if diagonal chaining were
ever turned off, this would have to follow it.

Simulated over 20,000 seeds: all three holes always place, no space is ever left with fewer than
two neighbours, and the board never splits into two regions.

⚠️ It also makes the dead-board lock likelier, for the plainest reason of all: fewer spaces and
fewer paths between them.

---

## 9. The Shusher

🚧 *Placeholder name.* **No items this round.** *(`NoConsumablesLibrarian` —
`Librarian_Shusher.asset`)*

Whatever the run is carrying stays in the box: dragging an item does nothing. **Tapping one still
opens its info box** — reading what you can't use is how you plan for the round after.

🎯 The one librarian aimed at the shop's consumable row. A run that leans on Doublers and Cheat
Sheets has to clear this round on its own words.

How it works: `Apply` sets `RoundRules.ConsumablesAllowed = false`, the mode reports it as
`GameMode.AllowsConsumables`, and `GameSession.CanUseConsumable` — the one place "may an item be
used now?" is answered — folds it in. The items area already obeys that, so it needed no change.
Leaving to the menu asks `IsAtRest`, not this, so pausing is unaffected.

---

## 10. The Prefacer

🚧 *Placeholder name.* **Every word must start on a marked tile.** *(`MarkedTileLibrarian`,
`atEnd` off — `count` 5, `minCount` 3, `stepEveryRounds` 6 — `Librarian_Prefacer.asset`)*

- **The count shrinks with the run**: 5 marked tiles, one fewer every 6 rounds, never below 3.
  On the librarian rounds that's 5, 5, 4, 4, 3, 3 …
- **Only the FIRST tile is checked.** A marked tile in the middle of a word is fine, as long as
  the word starts on another marked one.
- **It tries for different letters.** Each pick first looks for a tile whose letter isn't
  already marked, then for any tile that isn't a wild, then for anything.
- **Marks are topped up.** When a marked tile leaves the board — played, discarded, bombed,
  redrawn — a new one is marked as soon as the board settles, again preferring a fresh letter.
- **Marked tiles can be discarded** (his call, 2026-10-22 — first asked for "can't", then changed
  to match the infected tiles). It costs discards and the replacement is random. ⚠️ A mark you
  could never get rid of is how a round freezes with moves left: if no word starts on any marked
  tile, discarding one is the only way to change what's marked.
- **Items work as normal** on marked tiles; new marks follow.
- **The Cheat Sheet obeys it for free** — it asks the same `Refuse` — so dropping it on an
  unmarked tile finds nothing and isn't spent.

How it works: the librarian is still a stateless recipe. `Apply` only says **how many** and
**what kind** (`RoundRules.MarkCount` / `MarkKind`); the mode's **`TileMarker`** owns which tiles
are marked, because that's round state that changes as you play. A mark lives on the `Tile`
object (`Tile.Mark`), so it rides along through falls, shuffles and an Ink Blot. Picks walk the
board in a fixed order and draw from their own stream (`marks`), whose position is saved with the
marked cells — so a resumed round has the same marks and the next new one is the one that would
have come.

⚠️ 🚧 **The mark is a body tint** (green, `requiredTint` on the Tile prefab), and a SELECTED tile
shows the selection colour instead. If that makes the start tile hard to read mid-word, the next
step is a ring or a corner dot.

---

## 11. The Epilogist

🚧 *Placeholder name.* **Every word must END on a marked tile.** The Prefacer with `atEnd` on —
same class, same numbers, same everything else. *(`Librarian_Epilogist.asset`)*

🎯 It plays differently from its twin despite the shared code: you build *toward* a tile rather
than away from one, and the word row shows you whether you got there only at the last letter.

---

## 12. The Mildewed

🚧 *Placeholder name.* **Infected tiles take away what they would have added.**
*(`InfectedTileLibrarian` — `count` 3, `maxCount` 6, `stepEveryRounds` 6 —
`Librarian_Mildewed.asset`)*

- **Length:** an infected tile's letters come OFF the word's length. CRATE through an infected R
  counts as 3 (4 clean − 1) for length points and the length multiplier. The live preview shows it.
- **Points:** its letter value comes off the score, on its own beat after every clean tile.
- **Badges:** none of them fire — not 2L/3L, not 2W/3W.
- **The shorter length counts EVERYWHERE** (his call, 2026-10-22): a word whose shortened length
  is under the minimum is refused ("Infected letters make this too short"), and Marginalia and
  Shorthand read the shortened length (`ScoringContext.Length`).
- **The count grows with the run**: 3, one more every 6 rounds, capped at 6 — about a quarter of
  a 5×5 board. More than that and the board is just unplayable.
- **Infected tiles can be discarded** and new ones are infected to keep the count up — so
  discarding them costs discards and buys a different infected tile, not a clean board.

How it works: the marking is the same `TileMarker` as The Prefacer's, with `TileMark.Infected`.
The SCORING lives in Core, keyed off `Tile.Mark`, not in the librarian's `Score` hook — the length
has to be shortened before the walk opens, and a tile's badges have to be skipped on its own beat,
both of which happen inside `ScoreCalculator` before any librarian gets a turn.
`ChainController.ScoringLength` is the one place the shortened length is worked out; with no
infected tile it equals the plain letter count, which is why every other round scores exactly as
before.

**The penalties land AFTER every clean tile** (2026-10-26). The score is floored at 0 on every
step, so a penalty charged in place was partly forgiven when it came early in the word — the same
infected Z cost less at the start than at the end. Charged last, position changes nothing. A word
that is mostly infected still bottoms out at 0 rather than going negative.

**An empty bag ends the round once the CLEAN letters can't make a word.** `OutOfTiles` counts
`Board.ScoringLetterCount` (infected letters left out), so a board of leftover infected tiles is a
loss, not a round that never ends.

---

## Open

Questions that belong to the roster as a whole rather than to any one librarian.

- ⚠️ **A librarian round can lock.** If the bag is empty, no legal word is left on the board, and
  the discards are gone, nothing ends the round — moves only tick down when a word is played.
  The Grandiloquent, The Conformist and The Censor all make it likelier by refusing words, and
  The Redactor removes the discards that were the way out. The exits on the table are a forfeit
  button, counting a dead board as a loss, or detecting one. **Undecided.**
- ❓ **The Critic isn't previewed.** Bookmarks aren't previewed because the reveal is the payoff;
  a penalty has no payoff, and planning against a number that's always wrong is a different
  thing. Showing the round's rule in the live preview (while still hiding bookmarks) is a
  defensible split — the librarian's tax is imposed on you, your bookmarks are yours.
- ❓ **The shop promises a target it can't keep.** Its NEXT TARGET line reads the run's curve, but
  the next round's librarian isn't drawn until CONTINUE. Drawing it a round early would fix the
  line *and* let the shop announce who's next, Balatro-style; the alternative is for the line to
  stop claiming to know.
- ❓ **Nothing scales a librarian to the round it lands on.** The Insatiable's ×3 is the same
  demand on round 3 as on round 30, and The Critic's 25% is flat.
- ❓ **Every one of them is a restriction.** All twelve take something away; none gives anything
  back beyond the doubled payout. The Dilapidated is the first that changes the game rather than
  forbidding something — but a board with three holes in it is still a board with less on it. One
  that hands something over, or plays off a different bag, is still missing. `RoundRules` has room
  for it; the board's refill policy is the nearest untouched lever.
- ❓ **Does the neighbour rule want to be stricter than 2?** It stops a space being stranded, but
  a space with exactly two neighbours is still a near-dead corner of the board. 3 would push the
  holes further apart and make the round gentler; it's one number on the asset.
- ❓ **"Librarian" is a costume.** The noun lives in one config field *(`librarianLabel`)* and
  each name in its own asset, so the whole cast could become exams, critics or editors without
  touching the game. Nothing about the roster below assumes the library.

## Cut

- **The Cataloguer** — every word had to be a different length from every word before it.
  **Built, then cut on 2026-09-08 for being far too hard.** On a 5×5 board it tightened from
  both ends at once: after a 3 and a 5 you were hunting a specific length rather than the best
  word available, and by the fourth word the board usually couldn't offer one at all — which
  fed straight into the dead-board lock below. It was the exact inverse of The Conformist, and
  that pairing was the appeal; the lesson is that "same length every time" is a *choice* the
  player makes once, while "different length every time" is a constraint that compounds.
  `DistinctLengthLibrarian` and `Librarian_Cataloguer.asset` are recoverable from git history
  if a gentler version is ever wanted — a cap on how many lengths it tracks would be the knob.

## Pitched, not built

Kept here so they don't get re-invented from scratch:

- **The Overdue** — your discard allowance isn't refilled; you start with whatever you had left.
  Needs discards to carry between rounds, which they currently don't.
- **The Miser** — the round pays nothing. A pure tax: clear it and you gain only survival.
  Interesting because it inverts the "librarians pay double" promise.
- **The Levelled** — every word scores its base points; the length multiplier is ignored. Guts
  long words specifically. Wants the `Score` hook.
- **The Closed Stacks** — the board never refills. Twenty-five tiles and that's the round. Wants
  `IRefillPolicy` on `RoundRules`, which is the seam that already exists and is unused.
- **A harder Dilapidated** — the holes MOVE, closing a different set of spaces every few words.
  The plumbing is nearly all there (`Board.ClosedCells` plus a re-lay-out), but a hole that opens
  under a tile raises a question this one never has to answer: what happens to the tile.

---

*Adding one: `ARCHITECTURE.md` → "A librarian (a boss round)". Subclass `Librarian`, add a line
to `Assets/Editor/LibrarianSetup.cs`, run `Word Crush → Create Librarian Assets`, then write it
up here.*
