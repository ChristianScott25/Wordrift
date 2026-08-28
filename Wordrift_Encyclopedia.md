# 📖 The Wordrift Encyclopedia

> **The game, not the code.** How Wordrift is played, what the rules are, and what every
> number currently is. `ARCHITECTURE.md` explains how it's built — this explains what it *is*.

**Last updated:** 2026-08-27 · money, the buyable shop, and halved round targets
**Status:** playable demo in active design — the loop works end to end; the content doesn't exist yet

### How to read this

| Marker | Meaning |
|:--:|---|
| 🚧 | **Temporary.** Exists only so something can be played and seen. It *will* be replaced, and nothing should be balanced around it. |
| ❓ | **Undecided.** A real design question, deliberately left open. |
| 🎯 | **Intent.** What a rule is *for* — the feeling or decision it's meant to create. |

Every number here is authored in an asset and tunable in the Inspector; the file that owns it
is named in *(italics)*.

### Contents

1. [The game in one minute](#1-the-game-in-one-minute)
2. [How it's meant to be played](#2-how-its-meant-to-be-played)
3. [Making words](#3-making-words)
4. [Tiles](#4-tiles)
5. [The run](#5-the-run)
6. [Money](#6-money)
7. [The shop](#7-the-shop-)
8. [Modes](#8-modes)
9. [Every number, in one place](#9-every-number-in-one-place)
10. [Built · planned · open](#10-built--planned--open)

---

## 1. The game in one minute

You drag across a 5×5 grid of letters to spell words. A valid word explodes off the board and
new tiles fall in.

That's the arcade game. Wordrift wraps it in a **run**: a sequence of rounds, each one asking
for a score you have to reach inside a fixed number of words. Clear a round and you're paid;
spend the money in a shop; go again against a higher number. Miss it once and the run is over
and you start again from nothing.

The twist that makes it a roguelike rather than a word game with a timer: **you own your
letters.** A run gives you a sack of 98 tiles, and the shop lets you permanently improve
individual tiles inside it. The E you gild in round 1 is the same E that comes back to you in
round 5.

---

## 2. How it's meant to be played

### The moment-to-moment

You're looking at 25 letters and hunting for the best word you can *see*, not the best word
that exists. Every word you take rearranges the board underneath you, so the board you get
next is a consequence of the word you just played — take the long word across the bottom and
half the grid collapses; take a short one up top and almost nothing moves.

🎯 **The core tension is greed vs. progress.** You have 20 words. A three-letter word is
always available; a big multiplied one usually isn't yet. Spending a word on something small
is spending a scarce resource on almost nothing.

### The round

Every round is the same question with a bigger number: **can you reach the target before you
run out of words?**

You will almost always clear round 1 without thinking. That's on purpose — the early rounds
are where you're supposed to feel comfortable and bank money, because the targets grow by half
again every round after the third while your ability to score doesn't grow on its own. The
run kills you somewhere in the middle, when the target has outrun the letters you own.

🎯 **The moment the target is hit, the round stops.** Unused words are worth money, so a round
isn't "score as much as possible" — it's "get there fast". Overshooting the target buys you
nothing. That's the decision the round hinges on: keep grinding safe little words, or hold out
for the one big word that ends it early and pays for it.

### The run

Rounds don't reset you. Money carries. Upgrades carry. **Your sack is the only thing that
grows**, so the whole run is a race between the target curve going up and your tiles getting
better.

🎯 **The shop is where a run is actually won or lost.** Points come from good words, but good
words come from good tiles — and tiles only get better if you paid for it. A run that spends
nothing dies around the point the targets start compounding.

### What a session looks like

```
   MAIN MENU
       │
       ▼
   ROUND 1  ──── cleared ────►  💰 PAID  ──►  🛒 SHOP  ──┐
       │                                                 │
     missed                                              │
       │                                              ROUND 2  ──── cleared ──► …
       ▼                                                 │
   RUN OVER  ◄─────────────── missed ────────────────────┘
       │
       └──►  PLAY AGAIN  =  a brand new run, round 1, $0, a stock sack
```

A run is currently open-ended: there is no boss round and no "you win" — you play until a
target beats you. ❓ That ending is undesigned.

### Where the fun is supposed to come from

- **Recognition** — spotting a word nobody would find, in a grid that only exists for a second.
- **Compounding** — a tile you bought in round 1 paying off for the rest of the run.
- **Escalation** — the target curve going up faster than you're comfortable with.
- 🚧 The third one is doing most of the work right now, because there's very little to buy. The
  shop is the part of the design that most needs to become interesting.

---

## 3. Making words

**The board** is a 5×5 grid, always full at the start of a round. *(`Board_5x5.asset`)*

**To make a word**, drag across touching tiles. **Diagonals count**, so a tile has up to eight
neighbours. A tile can't be used twice in one word. Release to submit.

**A word is valid** if it's at least **3 letters** *(`minWordLength`)* and appears in the
dictionary — about 175,000 English words. An invalid word flashes red and **costs you
nothing**: no move, no penalty. *(`rejectedWordsCostMoves` — off)*

**When a word is accepted**, its tiles demolish, everything above them falls straight down,
and new tiles drop from the top to fill the gaps — for as long as the sack has tiles left to
give.

**Tiles in motion can't be grabbed.** A tile has to settle before it will respond to a drag,
so nothing slides out from under your finger mid-word.

### How a word is scored

Applied in this order:

| # | Step | Currently |
|:--:|---|---|
| 1 | Each tile's **base score** | Scrabble values — E=1, Q=10 |
| 2 | That tile's **letter multipliers** (2L, 3L), applied to its own value | stacks in order |
| 3 | Every **word multiplier** (2W, 3W) on the word, multiplied together | applies to the sum |
| 4 | **Length bonus** — points per letter past the minimum | **0 — off** |
| 5 | The mode's **score multiplier** | **×1** |

> **Worked example.** `CAT` = C(3) + A(1) + T(1) = **5**.
> Put a 2L on the C and it's (3×2) + 1 + 1 = **8**.
> Add a 2W anywhere in that word and it's **16**.

🎯 Length isn't rewarded yet *(step 4 is 0)*, so a long word is currently only worth what its
letters are worth. Whether long words should pay a premium is a live tuning question.

---

## 4. Tiles

A tile is **a letter, a base score, and any modifiers it carries** — and that identity belongs
to the tile itself, not to the letter. Two E's in your sack can be worth different amounts,
and only one of them may be gilded. This is what makes upgrading a *specific* tile meaningful.

**The catalog** *(`LetterSet_Scrabble.asset`)* defines every letter that can exist, what it's
worth, and how many of it are in a full sack. It's a Scrabble distribution: 26 letters, **98
tiles**, twelve E's, one Q, one Z.

### Modifiers

The badge on a tile. Four exist today:

| Badge | Effect | Shop price |
|:--:|---|--:|
| **2L** | doubles that tile's own letter score | $5 |
| **3L** | triples that tile's own letter score | $9 |
| **2W** | doubles the score of the whole word | $14 |
| **3W** | triples the score of the whole word | $22 |

The rules around them:

- **No tile ever spawns with a modifier.** The only way a tile has one is that you bought it,
  and it lasts the rest of the run.
- **A tile can carry several.** Letter multipliers stack in order (2L then 3L = ×6); word
  multipliers all multiply together. A stacked tile draws one badge per modifier, fanned to the
  right. 🚧 That fan is a first-pass visual, not the final treatment.
- **The number in a tile's corner is always its BASE score.** A 2L on an E still reads "1" —
  the badge is what tells you it doubles. This is deliberate: one number on the tile, one
  meaning.

🎯 Word multipliers are priced far above letter multipliers because they scale with the whole
word — a 3W on a common letter is the single most valuable thing in the shop.

❓ **Wild tiles** and **multi-letter tiles** ("QU", "IE") are both planned and neither is
designed. The catalog can already hold them; nothing can play them yet.

---

## 5. The run

A **run** is a sequence of rounds. It starts fresh from the main menu, and it ends the first
time you fail a round. Everything you earn and upgrade lives for exactly one run — **nothing
carries between runs**, and there's no meta-progression.

### Your sack of tiles

You own a sack of **98 tiles** — one full Scrabble set *(`sackCopies` = 1)*.

- Rounds draw from it **without replacement**. Once your E's are gone, no more E's arrive this
  round.
- **The opening board is paid for out of the sack** — 25 cells means a round starts having
  already spent a quarter of it.
- **The full sack comes back at the start of every round.** Playing tiles doesn't lose them;
  the sack belongs to the run, not the round.
- When it runs dry, tiles stop falling and the board plays down toward empty.
- **Upgrades change the sack itself.** That's why a tile you gild in round 1 keeps coming back.

🎯 The sack is the run's real character sheet, and the reason the shop matters. Making the
sack *smaller* (fewer, better tiles) or *bigger* is an obvious future upgrade axis — neither
exists yet.

### Rounds and targets

Each round: reach the target within **20 words** *(`moves`)*.

| Round | 1 | 2 | 3 | 4 | 5 | 6+ |
|---|--:|--:|--:|--:|--:|--:|
| **Target** | 30 | 45 | 65 | 98 | 146 | ×1.5 each round |

*(`roundTargets` and `targetGrowth`. 🚧 Halved on 2026-08-25 and halved **again** on
2026-08-27 for testing — they are deliberately very soft right now, and a run should be
expected to go long.)*

**A round ends the instant the target is reached** *(`endOnTargetReached`)*, banking your
unused words rather than playing them out.

### How a round ends

| Ending | What happened | Result |
|---|---|---|
| ✅ **Cleared** | Target reached | Paid, then the shop. Run continues. |
| ❌ **Out of moves** | 20 words used, target missed | **Run over.** |
| ❌ **Out of tiles** | Sack empty *and* too few tiles left on the board to make any word | **Run over.** |

That last one exists so a round can't stall forever with moves left and nothing to spend them
on. ❓ The softer version — tiles remain but no word can be made from them — isn't handled;
today you'd burn moves on rejects to escape it.

**PLAY AGAIN starts a completely new run**: round 1, a stock sack, $0.

---

## 6. Money

Earned per cleared round, kept for the whole run, spent in the shop.

**A cleared round pays:**

```
        $1  per 10 points scored        (pointsPerCoin = 10)
   +    $1  per unused move             (coinsPerUnusedMove = 1)
```

> **Worked example.** You clear round 1 with 33 points, on your 4th word of 20.
> `33 ÷ 10 = $3`, plus `16 unused moves = $16`. **Payout: $19.**

What that means in play:

- Because the round ends the moment you hit the target, the *points* half is roughly
  **target ÷ 10 every time**. The **unused-moves half is the part that varies** — and right
  now it's usually the bigger half.
- 🎯 So the economy pays for **efficiency, not for grinding.** Clearing in 6 words pays nearly
  double clearing in 15. Overshooting the target earns you nothing at all.
- 🚧 With the targets currently halved, the points half of the payout is small enough that
  money is *almost entirely* an unused-moves reward. That's a side effect of the test values,
  not the intent — expect to retune `pointsPerCoin` when the targets go back up.
- Money shows in the round HUD, but it can't change mid-round.
- Failing pays nothing, and the run's money dies with the run.

❓ No interest, no per-round purse, no sink other than the shop. Interest on savings — Balatro's
strongest economic hook — is an obvious next candidate.

---

## 7. The shop 🚧

Between rounds. It shows the round you cleared, what it paid, the next target, and what's for
sale. **CONTINUE** starts the next round.

> 🚧 **Everything about the STOCK below is placeholder.** The real shop's stock has to vary
> between visits, look different, and sell more than modifiers. This version exists only so the
> economy can be played end to end. The money and purchasing *machinery* around it is real.

🚧 **The stock is always the same four multipliers** — 2L, 3L, 2W, 3W — at the prices in §4.

🚧 **An upgrade lands on a random tile from your sack.** The tile is rolled when the shop opens
and shown on the button, so you can see what you're buying:

```
   2L → E      $5     [ BUY ]
   3L → Q      $9     [ BUY ]
   2W → A     $14     [ BUY ]
   3W → T     $22       $22      ← greyed out, can't afford
```

You never *choose* the tile. After each purchase that row rolls a different one.

🚧 **Re-buying the same option in one visit costs more each time** — ×1.5, rounded: $5 → $8 →
$12. Other rows are unaffected, and prices reset on the next visit.

🚧 **There's no reroll, no skip, and nothing else to spend on.** Unspent money simply carries.

❓ The real shop's open questions: what else is for sale (bookmarks, tiles, sack upgrades), how
stock is randomised, whether you choose which tile gets upgraded, and whether you can sell or
remove tiles.

---

## 8. Modes

| Mode | What it is |
|---|---|
| **Rogue Demo** | Everything described in this document. The mode the game is being built around. |
| **Moves** | A single arcade round — 20 words, endless tiles, no target, no run, no money. Kept as the simplest possible mode, and as proof a mode needs none of the roguelike systems. |

*Timed and Overflow modes were cut on 2026-08-25 — this is a roguelike now, not an arcade
collection.*

---

## 9. Every number, in one place

| | Value | Lives in |
|---|--:|---|
| Board | 5 × 5 | `Board_5x5.asset` |
| Minimum word length | 3 | `Mode_RogueDemo.asset` |
| Length bonus per extra letter | 0 (off) | `Mode_RogueDemo.asset` |
| Score multiplier | ×1 | `Mode_RogueDemo.asset` |
| Words per round | 20 | `Mode_RogueDemo.asset` |
| Invalid word costs a move | no | `Mode_RogueDemo.asset` |
| Move counter turns red at | 3 left | `Mode_RogueDemo.asset` |
| Round targets | 30 / 45 / 65, then ×1.5 | `Mode_RogueDemo.asset` |
| Sack size | 98 tiles (1 Scrabble set) | `Mode_RogueDemo.asset` + `LetterSet_Scrabble.asset` |
| Letter values & counts | Scrabble | `LetterSet_Scrabble.asset` |
| Points per $1 | 10 | `Mode_RogueDemo.asset` |
| $ per unused move | 1 | `Mode_RogueDemo.asset` |
| Re-buy price growth | ×1.5 | `Mode_RogueDemo.asset` |
| Modifier prices | 5 / 9 / 14 / 22 | each asset in `GameData/Modifiers/` |

---

## 10. Built · planned · open

### ✅ Built and playable

The board and word-making · scoring with stacking multipliers · the run (rounds, escalating
targets, a persistent finite sack) · money · a placeholder shop that sells permanent tile
upgrades.

### 📋 Decided, not built

- **Bookmarks** — the working name for Balatro-style relics that hook into scoring. Nothing
  exists yet.
- **Sacks with abilities**, boards of varying **size and shape**, and boards where **gravity
  flows differently**. All three have seams in the code; no content uses them.
- **Tile skins as a player-facing thing** — several looks can already share a board, but
  nothing decides which ones a player *has*.

### ❓ Open questions

- **Wild tiles** — a special letter, or a modifier?
- **Multi-letter tiles** ("QU") — the sack can hold them; nothing can play them.
- **Gravity on a board with holes** — tiles currently fall *past* gaps instead of into them.
- **How a run ends** — there's no boss round and no victory.
- **Reproducible runs** — every draw is unseeded, so sharing or replaying a seed isn't possible.
- **A board that's playable-looking but dead** — full of tiles that spell nothing (see §5).
- **The HUD** — round, target, sack and money are four readouts crammed into one line.
