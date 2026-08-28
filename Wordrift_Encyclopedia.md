# 📖 The Wordrift Encyclopedia

> **The game, not the code.** How Wordrift is played, what the rules are, and what every
> number currently is. `ARCHITECTURE.md` explains how it's built — this explains what it *is*.

**Last updated:** 2026-08-28 · the tile bag halved to 52 and renamed from "sack"; Moves mode cut
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
5. [Bookmarks](#5-bookmarks)
6. [The run](#6-the-run)
7. [Money](#7-money)
8. [The shop](#8-the-shop-)
9. [Modes](#9-modes)
10. [Every number, in one place](#10-every-number-in-one-place)
11. [Built · planned · open](#11-built--planned--open)

---

## 1. The game in one minute

You drag across a 5×5 grid of letters to spell words. A valid word explodes off the board and
new tiles fall in.

That's the arcade game. Wordrift wraps it in a **run**: a sequence of rounds, each one asking
for a score you have to reach inside a fixed number of words. Clear a round and you're paid;
spend the money in a shop; go again against a higher number. Miss it once and the run is over
and you start again from nothing.

The twist that makes it a roguelike rather than a word game with a timer: **you own your
letters.** A run gives you a bag of 52 tiles, and the shop lets you permanently improve
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

Rounds don't reset you. Money carries. Upgrades carry. **Your tile bag is the only thing
that grows**, so the whole run is a race between the target curve going up and your tiles
getting better.

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
       └──►  PLAY AGAIN  =  a brand new run, round 1, $0, a stock bag
```

A run is currently open-ended: there is no boss round and no "you win" — you play until a
target beats you. ❓ That ending is undesigned.

### Where the fun is supposed to come from

- **Recognition** — spotting a word nobody would find, in a grid that only exists for a second.
- **Compounding** — a tile or bookmark you bought in round 1 paying off for the rest of the run.
- **Escalation** — the target curve going up faster than you're comfortable with.
- 🚧 The shop is still the thinnest part: four tile upgrades and three bookmarks is not yet a
  space worth exploring. It's the part of the design that most needs to become interesting.

---

## 3. Making words

**The board** is a 5×5 grid, always full at the start of a round. *(`Board_5x5.asset`)*

**To make a word**, drag across touching tiles. **Diagonals count**, so a tile has up to eight
neighbours. A tile can't be used twice in one word. Release to submit.

**A word is valid** if it's at least **3 letters** *(`minWordLength`)* and appears in the
dictionary — about 175,000 English words. An invalid word flashes red and **costs you
nothing**: no move, no penalty. *(`rejectedWordsCostMoves` — off)*

**When a word is accepted**, its tiles demolish, everything above them falls straight down,
and new tiles drop from the top to fill the gaps — for as long as the bag has tiles left to
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
| 5 | **Your bookmarks**, each in turn, in the order you bought them | see §5 |
| 6 | The mode's **score multiplier** | **×1** |

> **Worked example.** `CAT` = C(3) + A(1) + T(1) = **5**.
> Put a 2L on the C and it's (3×2) + 1 + 1 = **8**.
> Add a 2W anywhere in that word and it's **16**.

🎯 Length isn't rewarded yet *(step 4 is 0)*, so a long word is currently only worth what its
letters are worth. Whether long words should pay a premium is a live tuning question.

---

## 4. Tiles

A tile is **a letter, a base score, and any modifiers it carries** — and that identity belongs
to the tile itself, not to the letter. Two E's in your bag can be worth different amounts,
and only one of them may be gilded. This is what makes upgrading a *specific* tile meaningful.

**The catalog** *(`LetterSet_Scrabble.asset`)* defines every letter that can exist, what it's
worth, and how common it is. It's the Scrabble distribution — vowel-heavy, one Q, one Z — but
those numbers are a **ratio, not a count**: the bag is built by sharing them out over however
many tiles the bag is meant to hold (see §6).

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

## 5. Bookmarks

**Bookmarks are the run's special abilities** — this game's version of Balatro's jokers. You
buy them in the shop, you keep them for the rest of the run, and they change how every word
scores from then on.

| Bookmark | What it does | Price |
|---|---|--:|
| **Bookend** | ×2 if the word starts and ends with the same letter | $12 |
| **Deja Vu** | +10 points for a word you already spelled **this round** | $10 |
| **Vowel Fanatic** | ×2 if the word has more vowels than consonants | $14 |

The rules around them:

- **One of each, at most.** The shop never offers a bookmark you already own, and there's no
  limit on how many different ones you can hold.
- **They stack.** Bookend and Vowel Fanatic both firing on the same word is **×4** — the same
  way tile multipliers already multiply together. `EYE` with both is a four-times word.
- **They fire in the order you bought them.** With these three that changes nothing, because
  doubling twice is doubling twice either way. It will start to matter the moment a bookmark
  *adds* to the multiplier instead of multiplying it — that's why the order is fixed and
  visible rather than arbitrary.
- **Bookmarks die with the run**, like money and tile upgrades.

Details worth knowing:

- **Vowel Fanatic treats Y as a consonant.** `YOYO` is 2 vowels against 2 consonants, so it
  doesn't fire; `AREA` (3 v 1) does. It needs *strictly* more, so an even split pays nothing.
- **Deja Vu counts repeats within a round only** — the list resets when a new round starts.
  Nothing in the game stops you playing the same word twice, so this turns a quirk into a
  tactic: spell `EYE`, then spell it again for +10.
- 🚧 **Nothing tells you when a bookmark fires.** The score just comes out higher. Feedback in
  the word popup is an obvious next addition.

❓ **Editions** — Balatro's holographic / negative / foil upgrades applied to a joker — are
planned but not built. A bookmark you own is already stored as its own object rather than as a
pointer to the shop's copy, specifically so two copies can differ later.

## 6. The run

A **run** is a sequence of rounds. It starts fresh from the main menu, and it ends the first
time you fail a round. Everything you earn and upgrade lives for exactly one run — **nothing
carries between runs**, and there's no meta-progression.

### Your tile bag

You own a bag of **52 tiles** *(`tileBagSize`)* — about half a Scrabble set, mixed in
Scrabble's proportions.

- Rounds draw from it **without replacement**. Once your E's are gone, no more E's arrive this
  round.
- **The opening board is paid for out of the bag** — 25 cells means a round starts having
  already spent nearly half of it, with 27 tiles held back to refill with.
- **The full bag comes back at the start of every round.** Playing tiles doesn't lose them;
  the bag belongs to the run, not the round.
- When it runs dry, tiles stop falling and the board plays down toward empty.
- **Upgrades change the bag itself.** That's why a tile you gild in round 1 keeps coming back.

**How the mix is decided.** The letter catalog's weights are a *ratio*, and the bag size is a
separate number; the bag is built by sharing the ratio out over that many tiles, **with a
floor of at least one of every letter**. At 98 that reproduces Scrabble exactly. At 52 the
floor is what bends it: J K Q X Z can't halve, so they take five tiles where their share says
two and a half, and the common letters pay the difference.

| | A | E | I | O | U | N R T | D L S | G | J K Q X Z |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| Scrabble (98) | 9 | 12 | 9 | 8 | 4 | 6 | 4 | 3 | 1 each |
| **Wordrift (52)** | **5** | **6** | **5** | **4** | **2** | **3** | **2** | **1** | **1 each** |

That holds the vowel share at 42% — the same as a real Scrabble bag — which is why the size
is 52 and not a flat 49. A true half would have dropped it to 39% and played noticeably drier.

⚠️ **The bag, not the move counter, is now what usually ends a round.** 52 tiles is at most
~16 three-letter words, against a 20-word allowance — so most rounds will end on *out of
tiles* rather than *out of moves*. That's a real shift in what the round is about, and one
Inspector field either way: raise `tileBagSize` to make moves matter again, or lower `moves`
to make the allowance bite first.

🎯 The bag is the run's real character sheet, and the reason the shop matters. Making it
*smaller* (fewer, better tiles) or *bigger* is an obvious future upgrade axis — `tileBagSize`
is exactly the number such an upgrade would turn, but nothing turns it yet.

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
| ❌ **Out of tiles** | Bag empty *and* too few tiles left on the board to make any word | **Run over.** |

That last one exists so a round can't stall forever with moves left and nothing to spend them
on. ❓ The softer version — tiles remain but no word can be made from them — isn't handled;
today you'd burn moves on rejects to escape it.

**PLAY AGAIN starts a completely new run**: round 1, a stock bag, $0.

---

## 7. Money

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

One shop visit is roughly **one bookmark or two tile upgrades**, which is the interesting
part: permanent scoring abilities and better letters compete for exactly the same money.

❓ No interest, no per-round purse, no sink other than the shop. Interest on savings — Balatro's
strongest economic hook — is an obvious next candidate.

---

## 8. The shop 🚧

Between rounds. It shows the round you cleared, what it paid, the next target, and what's for
sale. **CONTINUE** starts the next round.

> 🚧 **Everything about the STOCK below is placeholder.** The real shop's stock has to vary
> between visits, look different, and sell more than modifiers. This version exists only so the
> economy can be played end to end. The money and purchasing *machinery* around it is real.

🚧 **The stock is always the same four multipliers** — 2L, 3L, 2W, 3W — at the prices in §4.

🚧 **An upgrade lands on a random tile from your bag.** The tile is rolled when the shop opens
and shown on the button, so you can see what you're buying:

```
   BOOKMARKS   BOOKEND

   2L → E            $5     [ BUY ]
   3L → Q            $9     [ BUY ]
   2W → A           $14     [ BUY ]
   3W → T           $22       $22    ← greyed out, can't afford
   VOWEL FANATIC    $14     [ BUY ]  ← gone once you own them all
```

You never *choose* the tile. After each purchase that row rolls a different one.

🚧 **Re-buying the same option in one visit costs more each time** — ×1.5, rounded: $5 → $8 →
$12. Other rows are unaffected, and prices reset on the next visit.

**A fifth row sells one bookmark** — a random one you don't already own, picked when the shop
opens and fixed for that visit. Buy it and the row rolls a different one. **When you own every
bookmark the row simply isn't there**, and the shop carries on as normal. Bookmark prices
never escalate, since you can only buy each one once.

Your owned bookmarks are listed above the shelf, and again on their own line in the round HUD
during play.

🚧 **There's no reroll, no skip, and nothing else to spend on.** Unspent money simply carries.

❓ The real shop's open questions: what else is for sale (bookmarks, tiles, bag upgrades), how
stock is randomised, whether you choose which tile gets upgraded, and whether you can sell or
remove tiles.

---

## 9. Modes

| Mode | What it is |
|---|---|
| **Rogue Demo** | Everything described in this document. The only mode there is. |

*Timed and Overflow modes were cut on 2026-08-25, and Moves — the last arcade round — on
2026-08-28. This is a roguelike now, not an arcade collection. The code still has the seam a
second mode would slot into; there just isn't one.*

---

## 10. Every number, in one place

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
| Tile bag size | 52 tiles (~half a Scrabble set) | `Mode_RogueDemo.asset` |
| Letter values & mix | Scrabble proportions, floor of 1 each | `LetterSet_Scrabble.asset` |
| Points per $1 | 10 | `Mode_RogueDemo.asset` |
| $ per unused move | 1 | `Mode_RogueDemo.asset` |
| Re-buy price growth | ×1.5 | `Mode_RogueDemo.asset` |
| Modifier prices | 5 / 9 / 14 / 22 | each asset in `GameData/Modifiers/` |
| Bookmark prices | 12 / 10 / 14 | each asset in `GameData/Bookmarks/` |
| Deja Vu bonus | +10 | `DejaVu.asset` |
| Bookend / Vowel Fanatic multiplier | ×2 | their assets in `GameData/Bookmarks/` |

---

## 11. Built · planned · open

### ✅ Built and playable

The board and word-making · scoring with stacking multipliers · the run (rounds, escalating
targets, a persistent finite tile bag) · money · bookmarks (three of them, with a scoring pipeline
built to take many more) · a placeholder shop that sells permanent tile upgrades and one
bookmark a visit.

### 📋 Decided, not built

- **Tile bags with abilities**, boards of varying **size and shape**, and boards where **gravity
  flows differently**. All three have seams in the code; no content uses them.
- **Tile skins as a player-facing thing** — several looks can already share a board, but
  nothing decides which ones a player *has*.

### ❓ Open questions

- **Wild tiles** — a special letter, or a modifier?
- **Multi-letter tiles** ("QU") — the bag can hold them; nothing can play them.
- **Gravity on a board with holes** — tiles currently fall *past* gaps instead of into them.
- **How a run ends** — there's no boss round and no victory.
- **Reproducible runs** — every draw is unseeded, so sharing or replaying a seed isn't possible.
- **A board that's playable-looking but dead** — full of tiles that spell nothing (see §5).
- **The HUD** — round, target, bag and money share one line; bookmarks have their own below it.
- **Bookmark editions** — holographic / negative / foil equivalents are planned, undesigned.
- **Bookmark feedback** — nothing shows you *which* bookmark just paid out.
