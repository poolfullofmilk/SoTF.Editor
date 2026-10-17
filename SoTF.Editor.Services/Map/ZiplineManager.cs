using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Map;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Map;

public static class ZiplineManager
{
    private const int ZiplineTypeId = 55;

    public static ZiplinePoi? AcceptNewPosition(
        ZipPointPoi point,
        Savegame selectedSavegame,
        Position newPosition
    )
    {
        point.IsMoveRequested = false;
        if (point.IsZiplineCreationRequested)
        {
            point.IsZiplineCreationRequested = false;
            return CreateNew(selectedSavegame, point.Position!, newPosition) is { } newToken
                ? new ZiplinePoi(newToken, point.Position!, newPosition)
                : null;
        }

        var zipline = point.Parent;
        var posAOld = zipline.PointA.Position;
        var posBOld = zipline.PointB.Position;
        point.Position = newPosition;

        // A Failed Move Puts The Anchors Back
        if (Move(selectedSavegame, zipline, posAOld, posBOld))
        {
            return null;
        }

        zipline.PointA.Position = posAOld;
        zipline.PointB.Position = posBOld;
        return null;
    }

    public static JObject? CreateNew(Savegame savegame, Position anchorA, Position anchorB)
    {
        if (Load(savegame) is not (var ziplines, var structures))
        {
            return null;
        }

        var ziplineToken = new JObject
        {
            { "_anchorAPosition", JToken.FromObject(anchorA) },
            { "_anchorBPosition", JToken.FromObject(anchorB) },
            { "_canBeDismantled", true },
        };
        ziplines.Items.Add(ziplineToken);
        ziplines.Wrapper.MarkAsModified(Constants.JsonKeys.ZipLineManager);

        // The Line Also Lives As A Structure At Its Midpoint
        structures.Items.Add(
            new JObject
            {
                { "Id", ZiplineTypeId },
                { "Pos", JToken.FromObject(Midpoint(anchorA, anchorB)) },
                { "Storages", new JArray() },
            }
        );
        structures.Wrapper.MarkAsModified(Constants.JsonKeys.ScrewStructureInstances);

        return ziplineToken;
    }

    private static bool Move(
        Savegame savegame,
        ZiplinePoi zipline,
        Position? posAOld,
        Position? posBOld
    )
    {
        if (
            posAOld == null
            || posBOld == null
            || zipline.PointA.Position is not { } posANew
            || zipline.PointB.Position is not { } posBNew
            || Load(savegame) is not (var ziplines, var structures)
            || ziplines.Items.FirstOrDefault(token => token.Equals(zipline.Token))
                is not { } ziplineToken
        )
        {
            return false;
        }

        ziplineToken["_anchorAPosition"] = JToken.FromObject(posANew);
        ziplineToken["_anchorBPosition"] = JToken.FromObject(posBNew);
        ziplines.Wrapper.MarkAsModified(Constants.JsonKeys.ZipLineManager);
        zipline.Token = ziplineToken;

        if (FindStructure(structures.Items, posAOld, posBOld)?["Pos"] is { } pos)
        {
            var midpoint = Midpoint(posANew, posBNew);
            pos["x"] = midpoint.X;
            pos["y"] = midpoint.Y;
            pos["z"] = midpoint.Z;
            structures.Wrapper.MarkAsModified(Constants.JsonKeys.ScrewStructureInstances);
        }

        return true;
    }

    public static bool Remove(ZiplinePoi zipline, Savegame savegame)
    {
        if (
            zipline.PointA.Position is not { } posA
            || zipline.PointB.Position is not { } posB
            || Load(savegame) is not (var ziplines, var structures)
            || ziplines.Items.FirstOrDefault(token => token.Equals(zipline.Token))
                is not { } ziplineToken
        )
        {
            return false;
        }

        ziplines.Items.Remove(ziplineToken);
        ziplines.Wrapper.MarkAsModified(Constants.JsonKeys.ZipLineManager);

        if (FindStructure(structures.Items, posA, posB) is { } structure)
        {
            structures.Items.Remove(structure);
            structures.Wrapper.MarkAsModified(Constants.JsonKeys.ScrewStructureInstances);
        }

        return true;
    }

    private static (
        (SaveDataWrapper Wrapper, JArray Items) Ziplines,
        (SaveDataWrapper Wrapper, JArray Items) Structures
    )? Load(Savegame savegame)
    {
        if (
            savegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.ZipLineManagerSaveData)
                is not { } ziplineWrapper
            || ziplineWrapper.GetJsonBasedToken(Constants.JsonKeys.ZipLineManager)?["Ziplines"]
                is not JArray ziplines
            || savegame.SavegameStore.LoadJsonRaw(
                SavegameStore.FileType.ScrewStructureInstancesSaveData
            )
                is not { } structureWrapper
            || structureWrapper
                .GetJsonBasedToken(Constants.JsonKeys.ScrewStructureInstances)
                ?["_structures"]
                is not JArray structures
        )
        {
            return null;
        }

        return ((ziplineWrapper, ziplines), (structureWrapper, structures));
    }

    private static Position Midpoint(Position anchorA, Position anchorB)
    {
        return new Position(
            (anchorA.X + anchorB.X) / 2,
            (anchorA.Y + anchorB.Y) / 2,
            (anchorA.Z + anchorB.Z) / 2
        );
    }

    private static JToken? FindStructure(JArray structures, Position anchorA, Position anchorB)
    {
        // The Zipline Structure Sits Halfway Between Its Anchors
        var midpoint = Midpoint(anchorA, anchorB);
        return structures.FirstOrDefault(structure =>
            structure["Id"]?.Value<int>() == ZiplineTypeId
            && structure["Pos"] is { } pos
            && pos["x"]?.Value<float>() is { } x
            && pos["y"]?.Value<float>() is { } y
            && pos["z"]?.Value<float>() is { } z
            && Math.Abs(midpoint.X - x) < 0.01
            && Math.Abs(midpoint.Y - y) < 0.01
            && Math.Abs(midpoint.Z - z) < 0.01
        );
    }
}
