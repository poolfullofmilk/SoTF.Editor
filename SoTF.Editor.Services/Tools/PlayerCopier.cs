using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Services.Pages;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Tools;

public static class PlayerCopier
{
    // Stat Entries, Position And Area Stay Put
    private static readonly HashSet<string> s_statEntries =
    [
        "Strength",
        "StrengthLevel",
        "CurrentStrength",
        "RequiredCurrentStrengthPerLevel",
        "MaxHealth",
        "CurrentHealth",
        "TargetHealth",
        "Sickness",
        "Hydration",
        "HydrationBuff",
        "Fullness",
        "FullnessBuff",
        "Rest",
        "RestBuff",
        "Stamina",
    ];

    public static List<PlayerPart> Copy(
        string sourcePath,
        string targetPath,
        IReadOnlyCollection<PlayerPart> parts,
        string sourceName
    )
    {
        // As Last Saved, Unsaved Edits Stay In The Open Savegame
        var source = new SavegameStore(sourcePath);
        var target = new SavegameStore(targetPath);
        var wrappers = new Dictionary<SavegameStore.FileType, SaveDataWrapper>();
        var copied = new List<PlayerPart>();
        foreach (var part in parts)
        {
            var done = part switch
            {
                PlayerPart.Stats => CopyEntries(source, target, wrappers, s_statEntries.Contains),
                PlayerPart.Inventory => CopyDocument(
                    source,
                    target,
                    wrappers,
                    SavegameStore.FileType.PlayerInventorySaveData,
                    Constants.JsonKeys.PlayerInventory
                )
                    && CopyEntries(
                        source,
                        target,
                        wrappers,
                        name =>
                            name.StartsWith(
                                InventoryPageViewModel.BlueprintPagePrefix,
                                StringComparison.Ordinal
                            )
                    ),
                PlayerPart.Armor => CopyDocument(
                    source,
                    target,
                    wrappers,
                    SavegameStore.FileType.PlayerArmourSystemSaveData,
                    Constants.JsonKeys.PlayerArmourSystem
                ),
                _ => CopyDocument(
                    source,
                    target,
                    wrappers,
                    SavegameStore.FileType.PlayerClothingSystemSaveData,
                    Constants.JsonKeys.PlayerClothingSystem
                ),
            };
            if (done)
            {
                copied.Add(part);
            }
        }

        target.SaveWrappers(
            wrappers,
            new() { ["Player"] = [new Change(AppText.Format("CopiedPlayerFrom", sourceName))] }
        );
        return copied;
    }

    private static bool CopyDocument(
        SavegameStore source,
        SavegameStore target,
        Dictionary<SavegameStore.FileType, SaveDataWrapper> wrappers,
        SavegameStore.FileType fileType,
        string jsonKey
    )
    {
        if (
            source.LoadJsonRaw(fileType)?.GetJsonBasedToken(jsonKey) is not { } copy
            || target.LoadJsonRaw(fileType) is not { } targetWrapper
            || !targetWrapper.Replace(jsonKey, copy.DeepClone())
        )
        {
            return false;
        }

        wrappers[fileType] = targetWrapper;
        return true;
    }

    private static bool CopyEntries(
        SavegameStore source,
        SavegameStore target,
        Dictionary<SavegameStore.FileType, SaveDataWrapper> wrappers,
        Func<string, bool> copies
    )
    {
        const SavegameStore.FileType fileType = SavegameStore.FileType.PlayerStateSaveData;
        if (
            source
                .LoadJsonRaw(fileType)
                ?.GetJsonBasedToken(Constants.JsonKeys.PlayerState)
                ?["_entries"]
                is not JArray sourceEntries
            || target.LoadJsonRaw(fileType) is not { } targetWrapper
            || targetWrapper.GetJsonBasedToken(Constants.JsonKeys.PlayerState)?["_entries"]
                is not JArray targetEntries
        )
        {
            return false;
        }

        // In Place, A Zero Left Out Here Goes There Too
        var copiesByName = sourceEntries
            .Where(entry => copies(entry["Name"]?.ToString() ?? ""))
            .ToDictionary(entry => entry["Name"]!.ToString());
        foreach (
            var entry in targetEntries
                .Where(entry => copies(entry["Name"]?.ToString() ?? ""))
                .ToList()
        )
        {
            if (copiesByName.Remove(entry["Name"]!.ToString(), out var copy))
            {
                entry.Replace(copy.DeepClone());
            }
            else
            {
                entry.Remove();
            }
        }

        foreach (var copy in copiesByName.Values)
        {
            targetEntries.Add(copy.DeepClone());
        }

        targetWrapper.MarkAsModified(Constants.JsonKeys.PlayerState);
        wrappers[fileType] = targetWrapper;
        return true;
    }
}
