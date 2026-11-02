using NLog;

namespace SoTF.Editor.Services.Savegames;

public static class FileTransaction
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    public static void Commit(
        IReadOnlyList<(string Target, Action<string> Write)> writes,
        IReadOnlyList<string> removals
    )
    {
        // New Files Go To Names Only This Commit Owns
        var staged = new List<(string Temporary, string Target)>();
        try
        {
            foreach (var (target, write) in writes)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                staged.Add((OwnedName(target, "staged"), target));
                write(staged[^1].Temporary);
            }
        }
        catch
        {
            Discard(staged.Select(pair => pair.Temporary));
            throw;
        }

        // Old Files Step Aside, So A Failure Puts Them Back
        var asides = new List<(string Aside, string Original)>();
        var placed = new List<string>();
        try
        {
            foreach (
                var original in staged
                    .Select(pair => pair.Target)
                    .Concat(removals)
                    .Where(File.Exists)
            )
            {
                var aside = OwnedName(original, "replaced");
                File.Move(original, aside);
                asides.Add((aside, original));
            }

            foreach (var (temporary, target) in staged)
            {
                File.Move(temporary, target);
                placed.Add(target);
            }
        }
        catch (Exception exception)
        {
            RollBack(placed, asides, exception);
            Discard(staged.Select(pair => pair.Temporary));
            throw;
        }

        Discard(asides.Select(pair => pair.Aside));
    }

    public static void Replace(string target, Action<string> write)
    {
        Commit([(target, write)], []);
    }

    private static string OwnedName(string path, string purpose)
    {
        // A Fresh Id Matches No Save, Backup Or Older File
        return $"{path}.{Guid.NewGuid():N}.{purpose}";
    }

    private static void RollBack(
        List<string> placed,
        List<(string Aside, string Original)> asides,
        Exception failure
    )
    {
        try
        {
            placed.ForEach(File.Delete);
            asides.ForEach(pair => File.Move(pair.Aside, pair.Original));
        }
        catch (Exception rollbackFailure)
        {
            // The Aside Files Stay, They Hold The Old Contents
            s_logger.Error(rollbackFailure, "Failed To Undo A Partial Write");
            throw new IOException(
                $"Failed To Undo A Partial Write, Old Files Kept As {string.Join(", ", asides.Select(pair => Path.GetFileName(pair.Aside)))}",
                new AggregateException(failure, rollbackFailure)
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
