using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🚧 TEMPORARY — part of the Bookshop experiment (see BookshopScreen).
///
/// The Bookshop's sections, in the order they are drawn AND rolled.
///
/// ⚠️ APPEND ONLY. The value is three things at once: the order sections
/// appear top to bottom, the order their stock is rolled off the shop stream
/// (so part of the seed), and the index a run's upgrade level is saved under
/// (RunSaveData.shopLevels). Inserting one in the middle would re-point every
/// saved level at the wrong section.
/// </summary>
public enum ShopSection
{
    Bookmarks = 0,
    Consumables = 1,
    Tiles = 2,
    Checkouts = 3,
}

/// <summary>
/// 🚧 One section of the Bookshop and its upgrade ladder, as authored on the
/// mode config. Read-only like every authored asset — what a RUN has bought is
/// RunState's (its level per section), never this.
/// </summary>
[System.Serializable]
public class BookshopSection
{
    public ShopSection section;

    [Tooltip("Level 0 is how the section starts every run — 0 slots means it " +
             "starts LOCKED. Each level after it is one upgrade: what it costs " +
             "to reach and how many slots the section then has. Opening a locked " +
             "section is simply its first upgrade.")]
    public List<BookshopLevel> levels = new();
}

/// <summary>🚧 One rung of a Bookshop section's ladder.</summary>
[System.Serializable]
public class BookshopLevel
{
    [Tooltip("How many things this section offers each visit at this level. 0 = locked.")]
    [Min(0)] public int slots;

    [Tooltip("What it costs to REACH this level, before the shared step every " +
             "other upgrade adds. Ignored on level 0, which is free by definition.")]
    [Min(0)] public int price;

    public BookshopLevel() { }

    public BookshopLevel(int slots, int price)
    {
        this.slots = slots;
        this.price = price;
    }
}
