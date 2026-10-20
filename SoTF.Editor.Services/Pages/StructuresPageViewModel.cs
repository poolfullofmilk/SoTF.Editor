using Newtonsoft.Json.Linq;
using NLog;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.SaveData.Storage;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Pages;

public class StructuresPageViewModel
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    private readonly Dictionary<int, ScrewStructure> _structuresById;

    public StructuresPageViewModel(GameData gameData)
    {
        StructureTypes =
        [
            .. gameData
                .ScrewStructures.OrderBy(screwStructure => screwStructure.CategoryName)
                .ThenBy(screwStructure => screwStructure.Name),
        ];
        _structuresById = gameData.ScrewStructures.ToDictionary(structure => structure.Id);
    }

    public ScrewStructure? BatchSelectedStructureType { get; set; }

    public List<ScrewStructureWrapper> Structures { get; } = [];

    public bool HasChanges => Structures.Any(structure => structure.IsChanged);

    public IReadOnlyList<ScrewStructure> StructureTypes { get; }

    public void Load(Savegame? savegame)
    {
        Structures.Clear();
        if (savegame is { } selectedSavegame)
        {
            Structures.AddRange(
                LoadStructures(selectedSavegame, ScrewStructureOrigin.Unfinished)
                    .Concat(LoadStructures(selectedSavegame, ScrewStructureOrigin.Finished))
                    .OrderBy(wrapper => wrapper.Category)
                    .ThenBy(wrapper => wrapper.Name)
            );
        }
    }

    private List<ScrewStructureWrapper> LoadStructures(
        Savegame savegame,
        ScrewStructureOrigin origin
    )
    {
        var (fileType, jsonKey) =
            origin == ScrewStructureOrigin.Unfinished
                ? (
                    SavegameStore.FileType.ScrewStructureNodeInstancesSaveData,
                    Constants.JsonKeys.ScrewStructureNodeInstances
                )
                : (
                    SavegameStore.FileType.ScrewStructureInstancesSaveData,
                    Constants.JsonKeys.ScrewStructureInstances
                );
        if (
            savegame.SavegameStore.LoadJsonRaw(fileType)?.GetJsonBasedToken(jsonKey)?["_structures"]
            is not JArray structures
        )
        {
            return [];
        }

        var wrappers = new List<ScrewStructureWrapper>();
        for (var index = 0; index < structures.Count; index++)
        {
            var structure = structures[index];
            if (structure.Type == JTokenType.Null)
            {
                continue;
            }

            var screwStructureId = structure["Id"]?.Value<int>();
            var screwStructure = screwStructureId is { } id
                ? _structuresById.GetValueOrDefault(id)
                : null;
            if (screwStructure == null && screwStructureId != null)
            {
                s_logger.Debug($"Found Unknown {origin} Structure {screwStructureId}");
            }

            // A Finished Structure Is Fully Built
            var added =
                origin == ScrewStructureOrigin.Unfinished
                    ? structure["Added"]?.Value<int>() ?? 0
                    : screwStructure?.BuildCost ?? 0;
            wrappers.Add(
                new ScrewStructureWrapper(
                    screwStructure,
                    structure,
                    added,
                    structure["Pos"]?.ToObject<Position>(),
                    origin,
                    index
                )
            );
        }

        return wrappers;
    }

    public bool Update(Savegame savegame)
    {
        // A Row Already Moved Or Removed Has Left Its Array
        var modified = Structures
            .Where(wrapper =>
                wrapper.ModificationMode is not (null or ScrewStructureModificationMode.None)
                && wrapper.Token.Parent != null
            )
            .ToList();

        if (modified.Count == 0)
        {
            return false;
        }

        if (
            savegame.SavegameStore.LoadJsonRaw(
                SavegameStore.FileType.ScrewStructureNodeInstancesSaveData
            )
                is not { } unfinishedSaveDataWrapper
            || unfinishedSaveDataWrapper
                .GetJsonBasedToken(Constants.JsonKeys.ScrewStructureNodeInstances)
                ?["_structures"]
                is not JArray unfinishedStructures
        )
        {
            s_logger.Info("Skipped Saving Structures, Found No Node Instances");
            return false;
        }

        if (
            savegame.SavegameStore.LoadJsonRaw(
                SavegameStore.FileType.ScrewStructureInstancesSaveData
            )
                is not { } finishedSaveDataWrapper
            || finishedSaveDataWrapper
                .GetJsonBasedToken(Constants.JsonKeys.ScrewStructureInstances)
                ?["_structures"]
                is not JArray finishedStructures
        )
        {
            s_logger.Info("Skipped Saving Structures, Found No Instances");
            return false;
        }

        // Edits Change The Live Arrays, So Zipline Anchors Stay
        var unfinishedChanged = false;
        var finishedChanged = false;
        foreach (var wrapper in modified)
        {
            var (unfinished, finished) =
                wrapper.Origin == ScrewStructureOrigin.Unfinished
                    ? ApplyToUnfinished(wrapper, finishedStructures)
                    : ApplyToFinished(wrapper, unfinishedStructures);
            unfinishedChanged |= unfinished;
            finishedChanged |= finished;
        }

        if (unfinishedChanged)
        {
            unfinishedSaveDataWrapper.MarkAsModified(
                Constants.JsonKeys.ScrewStructureNodeInstances
            );
        }

        if (finishedChanged)
        {
            finishedSaveDataWrapper.MarkAsModified(Constants.JsonKeys.ScrewStructureInstances);
        }

        return unfinishedChanged || finishedChanged;
    }

    private static (bool Unfinished, bool Finished) ApplyToUnfinished(
        ScrewStructureWrapper wrapper,
        JArray finishedStructures
    )
    {
        var token = wrapper.Token;
        var id = wrapper.ChangedTypeId ?? token["Id"]?.Value<int>();
        switch (wrapper.ModificationMode)
        {
            case ScrewStructureModificationMode.Remove:
                token.Remove();
                return (true, false);
            case ScrewStructureModificationMode.Finish when id is { } finishedId:
                finishedStructures.Add(
                    JToken.FromObject(
                        new StorageSaveData
                        {
                            Id = finishedId,
                            Pos = wrapper.Position,
                            Rot = token["Rot"],
                        }
                    )
                );
                token.Remove();
                return (true, true);
            case ScrewStructureModificationMode.AlmostFinish
            or ScrewStructureModificationMode.Unfinish when id is { } unfinishedId:
                token["Id"] = unfinishedId;
                token["Added"] =
                    wrapper.ModificationMode == ScrewStructureModificationMode.AlmostFinish
                        ? wrapper.AlmostFinishedAdded
                        : 0;
                return (true, false);
            default:
                return (false, false);
        }
    }

    private static (bool Unfinished, bool Finished) ApplyToFinished(
        ScrewStructureWrapper wrapper,
        JArray unfinishedStructures
    )
    {
        var token = wrapper.Token;
        var id = wrapper.ChangedTypeId ?? token["Id"]?.Value<int>();
        switch (wrapper.ModificationMode)
        {
            case ScrewStructureModificationMode.Remove:
                token.Remove();
                return (false, true);
            case ScrewStructureModificationMode.AlmostFinish
            or ScrewStructureModificationMode.Unfinish
                when id is { } unfinishedId
                    && wrapper.Position is { } position
                    && token["Rot"] is { } rotation:
                var added =
                    wrapper.ModificationMode == ScrewStructureModificationMode.AlmostFinish
                        ? wrapper.AlmostFinishedAdded
                        : 0;
                unfinishedStructures.Add(
                    JToken.FromObject(
                        new UnfinishedGenericScrewStructure(unfinishedId, position, rotation, added)
                    )
                );
                token.Remove();
                return (true, true);
            default:
                return (false, false);
        }
    }

    public void SetModificationMode(
        IEnumerable<ScrewStructureWrapper> rows,
        ScrewStructureOrigin origin,
        ScrewStructureModificationMode mode
    )
    {
        // Only The Rows The Search Shows
        foreach (
            var wrapper in rows.Where(structure =>
                structure.Origin == origin
                && (
                    BatchSelectedStructureType == null
                    || structure.ScrewStructure?.Id == BatchSelectedStructureType.Id
                )
            )
        )
        {
            wrapper.ModificationMode = mode;
        }
    }
}
