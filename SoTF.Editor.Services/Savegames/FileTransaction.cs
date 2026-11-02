using NLog;

namespace SoTF.Editor.Services.Savegames;

public static class FileTransaction
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    public static void Commit(
        IReadOnlyList<(string Target, bool Replaces, Action<string> Write)> writes,
        IReadOnlyList<string> removals
    )
    {
        // New Files Go To Names Only This Commit Owns
        var staged = new List<(string Temporary, string Target, bool Replaces)>();
        try
        {
            foreach (var (target, replaces, write) in writes)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                staged.Add((OwnedName(target, "staged"), target, replaces));
                write(staged[^1].Temporary);
            }
        }
        catch
        {
            Discard(staged.Select(pair => pair.Temporary));
            throw;
        }

        // One File At A Time, Old Ones Kept Aside
        var done = new List<(string Target, string? Aside)>();
        try
        {
            foreach (var (temporary, target, replaces) in staged)
            {
                if (replaces)
                {
                    // A Replaced File Gone Since Loading Fails Here
                    var aside = OwnedName(target, "replaced");
                    done.Add((target, aside));
                    File.Replace(temporary, target, aside);
                }
                else
                {
                    // A New File Never Lands On One That Appeared Meanwhile
                    File.Move(temporary, target);
                    done.Add((target, null));
                }
            }

            foreach (var original in removals)
            {
                var aside = OwnedName(original, "replaced");
                File.Move(original, aside);
                done.Add((original, aside));
            }
        }
        catch (Exception exception)
        {
            RollBack(done, exception);
            Discard(staged.Select(pair => pair.Temporary));
            throw;
        }

        Discard(done.Select(pair => pair.Aside).OfType<string>());
    }

    public static void ReplaceSingle(string target, bool mustExist, Action<string> write)
    {
        // One Rename Swaps It, The Name Never Points At Nothing
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temporary = OwnedName(target, "staged");
        try
        {
            write(temporary);
            if (mustExist && !File.Exists(target))
            {
                throw new IOException($"Found No {Path.GetFileName(target)} To Replace");
            }

            File.Move(temporary, target, true);
        }
        catch
        {
            Discard([temporary]);
            throw;
        }
    }

    private static string OwnedName(string path, string purpose)
    {
        // A Fresh Id Matches No Save, Backup Or Older File
        return $"{path}.{Guid.NewGuid():N}.{purpose}";
    }

    private static void RollBack(List<(string Target, string? Aside)> done, Exception failure)
    {
        // Newest First, Every Step Tried Even After One Fails
        var failures = new List<Exception>();
        var kept = new List<string>();
        foreach (var (target, aside) in Enumerable.Reverse(done))
        {
            try
            {
                if (aside is null)
                {
                    File.Delete(target);
                }
                else if (File.Exists(aside))
                {
                    File.Move(aside, target, true);
                }
            }
            catch (Exception rollbackFailure)
                when (rollbackFailure is IOException or UnauthorizedAccessException)
            {
                failures.Add(rollbackFailure);
                kept.Add($"{Path.GetFileName(target)} <- {aside ?? "(delete)"}");
            }
        }

        if (failures.Count > 0)
        {
            // The Aside Files Stay, The Message Maps Each Back
            s_logger.Error(
                failures[0],
                $"Failed To Undo A Partial Write: {string.Join("; ", kept)}"
            );
            throw new IOException(
                $"Failed To Undo A Partial Write, Recover By Hand: {string.Join("; ", kept)}",
                new AggregateException([failure, .. failures])
            );
        }
    }

    private static void Discard(IEnumerable<string> owned)
    {
        foreach (var path in owned)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception exception)
                when (exception is IOException or UnauthorizedAccessException)
            {
                // A Leftover Never Hides What Already Happened
                s_logger.Warn(exception, $"Failed To Remove {Path.GetFileName(path)}");
            }
        }
    }
}
