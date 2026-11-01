using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Services.Actors;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Tools;

public static class WorldTools
{
    public static string ResetTraps(Savegame? savegame)
    {
        if (
            savegame is not { } selectedSavegame
            || selectedSavegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.ScrewTrapsSaveData)
                is not { } saveDataWrapper
            || saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.ScrewTraps) is not { } traps
            || traps["_tarpStructureData"] is not JArray structureData
            || structureData.Count == 0
        )
        {
            return AppText.Get("NothingToReset");
        }

        var countReset = 0;
        foreach (var trapData in structureData)
        {
            var wasReset = false;
            if (trapData["IsTriggered"]?.Value<bool>() == true)
            {
                trapData["IsTriggered"] = false;
                wasReset = true;
            }

            if (trapData["Data1"]?.Value<int>() < 5)
            {
                trapData["Data1"] = 5;
                wasReset = true;
            }

            // A Trap Counts Once, However Much Of It Was Reset
            countReset += wasReset ? 1 : 0;
        }

        if (countReset == 0)
        {
            return AppText.Get("NothingToReset");
        }

        saveDataWrapper.MarkAsModified(Constants.JsonKeys.ScrewTraps);
        return AppText.Format("TrapsReset", countReset);
    }

    public static string ResetStructureDamage(Savegame? savegame)
    {
        if (
            savegame is not { } selectedSavegame
            || selectedSavegame.SavegameStore.LoadJsonRaw(
                SavegameStore.FileType.StructureDestructionSaveData
            )
                is not { } saveDataWrapper
            || saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.StructureDestruction)
                is not { } structureDestruction
            || structureDestruction["Data"] is not JArray { Count: > 0 } destructionData
        )
        {
            return AppText.Get("NoStructureDamage");
        }

        var entryCount = destructionData.Count;
        destructionData.Clear();
        saveDataWrapper.MarkAsModified(Constants.JsonKeys.StructureDestruction);

        return AppText.Format("StructureDamageRepaired", entryCount);
    }

    // A Sled Takes 1,000 Ml, Every Printer Holds That
    public const float PrinterResin = 1000;

    public static readonly SavegameStore.FileType[] Printers =
    [
        SavegameStore.FileType.Resin3dPrinterBunkerASaveData,
        SavegameStore.FileType.Resin3dPrinterBunkerBSaveData,
        SavegameStore.FileType.Resin3dPrinterEntertainmentSaveData,
        SavegameStore.FileType.Resin3dPrinterResidentialSaveData,
        SavegameStore.FileType.Resin3dPrinterResistanceSaveData,
    ];

    public static string PrinterKeyOf(SavegameStore.FileType printer)
    {
        return "Data." + printer.GetFilename().Replace("SaveData.json", "");
    }

    public static string RipenGardens(Savegame? savegame)
    {
        if (
            savegame?.SavegameStore.LoadJsonRaw(SavegameStore.FileType.GardenPlotManagerSaveData)
                is not { } saveDataWrapper
            || saveDataWrapper
                .GetJsonBasedToken(Constants.JsonKeys.GardenPlotManager)
                ?["_gardenPlotsData"]
                is not JArray plots
        )
        {
            return AppText.Get("NothingGrowing");
        }

        // Time Left Counts Down, A Plant Below Zero Is Ripe
        var ripened = 0;
        foreach (var plot in plots)
        {
            if (
                plot["SeedId"]?.Value<int>() > 0
                && plot["GrowTimeSecondsRemaining"]?.Value<float>() > 0
            )
            {
                plot["GrowTimeSecondsRemaining"] = -1f;
                ripened++;
            }
        }

        if (ripened == 0)
        {
            return AppText.Get("NothingGrowing");
        }

        saveDataWrapper.MarkAsModified(Constants.JsonKeys.GardenPlotManager);
        return AppText.Format("GardensRipened", ripened);
    }

    private static JArray? GuestList(Savegame? savegame) =>
        savegame
            ?.SavegameStore.LoadJsonRaw(SavegameStore.FileType.MultiplayerPlayerRolesSaveData)
            ?.GetJsonBasedToken(Constants.JsonKeys.MultiplayerPlayerRoles)
            ?["PlayerList"] as JArray;

    // Players Who Joined The Host, The Host Not Among Them
    public static List<Guest> ReadGuests(Savegame? savegame)
    {
        return
        [
            .. (GuestList(savegame) ?? [])
                .Select(player => new Guest(
                    player["PlayerId"]?.Value<long>() ?? 0,
                    player["LastKnownPlayerName"]?.ToString() ?? "",
                    player["PlayerRole"]?.Value<int>() ?? 0
                ))
                .OrderBy(guest => guest.Name, StringComparer.CurrentCultureIgnoreCase),
        ];
    }

    public static string SetGuestRoles(Savegame? savegame, IReadOnlyDictionary<long, int> roles)
    {
        var changed = 0;
        foreach (var player in GuestList(savegame) ?? [])
        {
            if (
                player["PlayerId"]?.Value<long>() is { } steamId
                && roles.TryGetValue(steamId, out var role)
                && player["PlayerRole"]?.Value<int>() != role
            )
            {
                player["PlayerRole"] = role;
                changed++;
            }
        }

        if (changed == 0)
        {
            return AppText.Get("NothingToChange");
        }

        savegame!
            .SavegameStore.LoadJsonRaw(SavegameStore.FileType.MultiplayerPlayerRolesSaveData)!
            .MarkAsModified(Constants.JsonKeys.MultiplayerPlayerRoles);
        return AppText.Format("GuestRolesChanged", changed);
    }

    public static string FillPrinters(Savegame? savegame)
    {
        var filled = 0;
        foreach (var printer in Printers)
        {
            var key = PrinterKeyOf(printer);
            if (
                savegame?.SavegameStore.LoadJsonRaw(printer) is not { } saveDataWrapper
                || saveDataWrapper.GetJsonBasedToken(key) is not JObject state
                || (state["CurrentResinVolume"]?.Value<float>() ?? 0) >= PrinterResin
            )
            {
                continue;
            }

            state["CurrentResinVolume"] = PrinterResin;
            saveDataWrapper.MarkAsModified(key);
            filled++;
        }

        return filled == 0 ? AppText.Get("PrintersFull") : AppText.Format("PrintersFilled", filled);
    }

    public static string EternalFires(Savegame? savegame)
    {
        return ChangeFires(savegame, 65535f, 0.00001f);
    }

    public static string ResetFires(Savegame? savegame)
    {
        return ChangeFires(savegame, 300f, 1.0f);
    }

    private static string ChangeFires(Savegame? savegame, float fuel, float fuelDrainRate)
    {
        if (
            savegame is not { } selectedSavegame
            || selectedSavegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.FiresSaveData)
                is not { } saveDataWrapper
            || saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.Fires) is not { } fires
            || fires["FiresPerStructureType"] is not { } firesPerStructureType
        )
        {
            return AppText.Get("NoFiresFound");
        }

        var countFound = 0;
        var countChanged = 0;
        foreach (
            var fire in firesPerStructureType
                .SelectMany(fireType => fireType.Children())
                .SelectMany(fires => fires)
        )
        {
            countFound++;

            // A Fire Already Burning That Way Is Not A Change
            var wasChanged = fire["IsLit"]?.Value<bool>() != true;
            fire["IsLit"] = true;
            wasChanged |= JsonModifier.CompareAndModify(fire, "Fuel", value => value != fuel, fuel);
            wasChanged |= JsonModifier.CompareAndModify(
                fire,
                "FuelDrainRate",
                value => value != fuelDrainRate,
                fuelDrainRate
            );
            countChanged += wasChanged ? 1 : 0;
        }

        if (countFound == 0)
        {
            return AppText.Get("NoFiresFound");
        }

        if (countChanged == 0)
        {
            return AppText.Get("NothingToChange");
        }

        saveDataWrapper.MarkAsModified(Constants.JsonKeys.Fires);
        return AppText.Format("FiresChanged", countChanged);
    }
}
