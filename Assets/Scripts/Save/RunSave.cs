using System.IO;
using UnityEngine;

/// <summary>
/// The only thing in the project that touches the disk. One slot, one file, no
/// prompt: a run saves itself in the background and the player never chooses to.
///
/// Every read is forgiving and every failure is the same failure — there is no
/// save. A corrupt file, a file from another format version, a missing directory:
/// all of them just mean CONTINUE doesn't appear. Nothing here may throw into
/// gameplay, because the alternative to a lost run is a game that won't start.
/// </summary>
public static class RunSave
{
    /// <summary>
    /// Bump this whenever RunSaveData's shape changes in a way an old file can't
    /// satisfy. Old saves are DISCARDED, not migrated — during a demo a lost run
    /// is cheaper than a migration path nobody will maintain.
    /// </summary>
    public const int Version = 1;

    private const string FileName = "run.json";

    private static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);
    private static string TempPath => Path + ".tmp";

    /// <summary>Is there a file at all? Says nothing about whether it will load.</summary>
    private static bool Exists => File.Exists(Path);

    /// <summary>
    /// Writes the run out, replacing whatever was there.
    ///
    /// Written to a temp file and RENAMED over the old one, which is a single
    /// atomic step — so being killed mid-write leaves the previous save intact
    /// rather than a half-written one, and there is never a moment when neither
    /// file exists. (Delete-then-move would have that moment, and a kill inside
    /// it would lose the run outright.) That matters more here than usual:
    /// saving happens after every move, so a write is in flight a fair fraction
    /// of the time the app might be killed.
    /// </summary>
    public static void Write(RunSaveData data)
    {
        if (data == null) return;
        data.version = Version;

        try
        {
            File.WriteAllText(TempPath, JsonUtility.ToJson(data));

            // Move, not copy: File.Replace needs the destination to exist, and
            // Delete-then-Move is the portable form Unity's players all support.
            if (File.Exists(Path)) File.Delete(Path);
            File.Move(TempPath, Path);
        }
        catch (IOException error)
        {
            Debug.LogWarning($"Couldn't save the run: {error.Message}");
        }
    }

    /// <summary>
    /// The saved run, or null when there isn't a usable one. Null covers every
    /// failure — no file, unreadable, unparseable, wrong version — because the
    /// caller can do exactly one thing about any of them.
    /// </summary>
    public static RunSaveData Read()
    {
        if (!Exists) return null;

        try
        {
            var data = JsonUtility.FromJson<RunSaveData>(File.ReadAllText(Path));
            if (data == null)
            {
                Debug.LogWarning("The saved run couldn't be read, so it's been ignored.");
                return null;
            }

            if (data.version != Version)
            {
                Debug.Log($"Saved run is format v{data.version}, this build reads v{Version} — discarding it.");
                return null;
            }

            return data;
        }
        catch (System.Exception error)
        {
            // Deliberately broad: a save file is untrusted input, and any way it
            // can be malformed has the same answer.
            Debug.LogWarning($"The saved run couldn't be read ({error.Message}), so it's been ignored.");
            return null;
        }
    }

    /// <summary>Throws the save away. Called when a run ends, however it ends.</summary>
    public static void Delete()
    {
        try
        {
            if (File.Exists(Path)) File.Delete(Path);
            if (File.Exists(TempPath)) File.Delete(TempPath);
        }
        catch (IOException error)
        {
            Debug.LogWarning($"Couldn't delete the saved run: {error.Message}");
        }
    }
}
