#:project ../SoTF.Editor.Services/SoTF.Editor.Services.csproj
#:package Microsoft.Extensions.DependencyInjection@10.0.13
#:property NoWarn=$(NoWarn);IL2026;IL3050
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Map;
using SoTF.Editor.Data.Models.Storage;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Services.Actors;
using SoTF.Editor.Services.Map;
using SoTF.Editor.Services.Pages;
using SoTF.Editor.Services.Savegames;

// The Sample Save, Copied So The Test Never Touches It
const string SampleSteamId = "76561190000000000";
var work = Path.Combine(Path.GetTempPath(), "SoTF.Editor.Tests", Guid.NewGuid().ToString("N"));
CopyDirectory(Path.Combine(SourceDirectory(), "Sample"), work);
var savegamePath = Path.Combine(work, SampleSteamId, "Multiplayer", "1000000001");
var settingsPath = Path.Combine(AppInfo.DataDirectory, "settings.json");
var settingsBefore = File.Exists(settingsPath) ? File.ReadAllBytes(settingsPath) : null;
var failures = new List<string>();

try
{
    var gameData = JsonConvert.DeserializeObject<GameData>(ResourceFile.Read("GameData.json"))!;
    var services = new ServiceCollection();
    services.AddSingleton(gameData);
    services.AddSingleton<StorageFactory>();
    services.AddSingleton<PlayerPageViewModel>();
    services.AddSingleton<ArmorPageViewModel>();
    services.AddSingleton<InventoryPageViewModel>();
    services.AddSingleton<StoragePageViewModel>();
    services.AddSingleton<FollowerPageViewModel>();
    services.AddSingleton<GameSetupPageViewModel>();
    services.AddSingleton<GameStatePageViewModel>();
    services.AddSingleton<WeatherPageViewModel>();
    services.AddSingleton<NpcsPageViewModel>();
    services.AddSingleton<StructuresPageViewModel>();
    services.AddSingleton<MainViewModel>();
    services.AddSingleton<ActorModifier>();
    services.AddSingleton<MapManager>();
    services.AddSingleton<PoiLoader>();
    services.AddSingleton<MapViewModel>();
    using var provider = services.BuildServiceProvider();
    var main = provider.GetRequiredService<MainViewModel>();
    var player = provider.GetRequiredService<PlayerPageViewModel>();
    var armor = provider.GetRequiredService<ArmorPageViewModel>();
    var inventory = provider.GetRequiredService<InventoryPageViewModel>();
    var storage = provider.GetRequiredService<StoragePageViewModel>();
    var followers = provider.GetRequiredService<FollowerPageViewModel>();
    var setup = provider.GetRequiredService<GameSetupPageViewModel>();
    var gameState = provider.GetRequiredService<GameStatePageViewModel>();
    var weather = provider.GetRequiredService<WeatherPageViewModel>();
    var npcs = provider.GetRequiredService<NpcsPageViewModel>();
    var structures = provider.GetRequiredService<StructuresPageViewModel>();
    var actorModifier = provider.GetRequiredService<ActorModifier>();
    var map = provider.GetRequiredService<MapViewModel>();

    // Nothing Open Shows No Changes, Before And After A Load
    string[] pageRoutes =
    [
        "Player",
        "Inventory",
        "Storage",
        "Followers",
        "Game",
        "Npcs",
        "Structures",
    ];
    Check(pageRoutes.All(route => !main.HasChanges(route)), "No Savegame Shows No Changes");

    var pristine = HashFiles(savegamePath);
    main.SelectSavegame(new Savegame(savegamePath, "1000000001", new SavegameStore(savegamePath)));

    // A Save Without Edits Writes Nothing And Shows No Changes
    Check(pageRoutes.All(route => !main.HasChanges(route)), "Fresh Load Shows No Changes");
    Check(!main.SaveChanges(), "No Edits Report No Changes");
    Check(SameHashes(pristine, HashFiles(savegamePath)), "No Edits Leave Every File Identical");

    // One Edit Per Page, Each Saved, Reloaded And Read Back
    EditAndCheck(
        "Player",
        "Player",
        () => player.PlayerState.Fullness = 42,
        () => Math.Abs(player.PlayerState.Fullness - 42) < 0.01
    );

    EditAndCheck(
        "Armour",
        "Player",
        armor.SetAllToMax,
        () =>
            armor.Armour.All(piece =>
                piece.ArmourPiece.RemainingArmourpoints == piece.MaxDurability
            )
    );

    var added = inventory.Filter(null, null, false).First(item => !inventory.IsHeld(item));
    EditAndCheck(
        "Inventory",
        "Inventory",
        () => inventory.AddUnassignedItem(added),
        () => inventory.Inventory.Any(item => item.Id == added.Id)
    );

    var (collectionIndex, storageIndex) = FirstFillableStorage(storage);
    var fillItemId = 0;
    EditAndCheck(
        "Storage",
        "Storage",
        () =>
        {
            var target = storage.StorageCollections[collectionIndex].Storages[storageIndex];
            fillItemId = target.SupportedItems[0].Item.Id;
            StoragePageViewModel.FillAll(target, target.SupportedItems[0], 1);
        },
        () =>
            StoragePageViewModel
                .StoredItemsOf(storage.StorageCollections[collectionIndex].Storages[storageIndex])
                .Any(item => item.SelectedItem?.Item.Id == fillItemId)
    );

    EditAndCheck(
        "Followers",
        "Followers",
        () => followers.KelvinState.Health = 77,
        () => Math.Abs(followers.KelvinState.Health - 77) < 0.01
    );

    var kelvinCount = followers.CopiesOf(Constants.Actors.KelvinTypeId).Count;
    EditAndCheck(
        "Follower Spawn",
        "Followers",
        () => main.SpawnFollowers(Constants.Actors.KelvinTypeId, 1),
        () => followers.CopiesOf(Constants.Actors.KelvinTypeId).Count == kelvinCount + 1
    );

    EditAndCheck(
        "Follower Remove",
        "Followers",
        () => main.RemoveFollower(followers.CopiesOf(Constants.Actors.KelvinTypeId)[^1]),
        () => followers.CopiesOf(Constants.Actors.KelvinTypeId).Count == kelvinCount
    );

    EditAndCheck(
        "Game Setup",
        "Game",
        () => setup.SetString(Constants.GameSetupKeys.EnemyHealth, "High", "NORMAL"),
        () => setup.GetString(Constants.GameSetupKeys.EnemyHealth, "") == "High"
    );

    var days = SettingOf(gameState.Settings, "GameDays").IntValue ?? 0;
    EditAndCheck(
        "Game State",
        "Game",
        () => SettingOf(gameState.Settings, "GameDays").IntValue = days + 1,
        () => SettingOf(gameState.Settings, "GameDays").IntValue == days + 1
    );

    var offset = SettingOf(weather.Settings, "_startingDayOffset").DecimalValue ?? 0;
    EditAndCheck(
        "Weather",
        "Game",
        () => SettingOf(weather.Settings, "_startingDayOffset").DecimalValue = offset + 5,
        () =>
            Math.Abs(
                (SettingOf(weather.Settings, "_startingDayOffset").DecimalValue ?? 0) - offset - 5
            ) < 0.001
    );

    var actor = npcs.AllActors.First(candidate =>
        candidate.ActorType?.IsFollower() != true && candidate.Stats?.ContainsKey("Health") == true
    );
    var actorId = actor.UniqueId;
    EditAndCheck(
        "NPCs",
        "Npcs",
        () =>
        {
            var edit = new EditActorViewModel(actor, gameData.ActorTypes) { ActorSelection = 0 };
            edit.ModifyOptions.ActorHealth = 33;
            main.ModifyActors(save => actorModifier.Modify(save, edit));
        },
        () =>
            npcs.AllActors.First(candidate => candidate.UniqueId == actorId).Stats?["Health"] == 33
    );

    var structureCount = structures.Structures.Count;
    EditAndCheck(
        "Structures",
        "Structures",
        () =>
            structures
                .Structures.First(wrapper =>
                    wrapper.ModificationModes.Contains(ScrewStructureModificationMode.Remove)
                )
                .ModificationMode = ScrewStructureModificationMode.Remove,
        () => structures.Structures.Count == structureCount - 1
    );

    EditAndCheck(
        "Game Preset",
        "Game",
        () => setup.ApplyPreset(GameSetupPageViewModel.HardGameMode),
        () =>
            setup.SelectedMode == GameSetupPageViewModel.CustomGameMode
            && setup.GetString(Constants.GameSetupKeys.AnimalSpawnRate, "")
                == GameSetupPageViewModel.SettingValueLOW
    );

    var target = new Position(-900, 120, 850) { Area = player.PlayerState.Pos.Area };
    EditAndCheck(
        "Map Teleport",
        "Player",
        () => player.PlayerState.Pos = target,
        () => player.PlayerState.Pos.HasSameCoordinates(target)
    );

    // A Removed Zipline Lights The Map And Saves
    map.Build();
    var ziplineCount = map.AllPois().OfType<ZiplinePoi>().Count();
    EditAndCheck(
        "Zipline",
        "Map",
        () => map.RemoveZipline(map.AllPois().OfType<ZiplinePoi>().First()),
        () =>
        {
            map.Build();
            return map.AllPois().OfType<ZiplinePoi>().Count() == ziplineCount - 1;
        }
    );

    // A Setting Switched And Switched Back Leaves Nothing To Save
    var paused = setup.GetBool(Constants.GameSetupKeys.PausedInInventory, false);
    setup.SetBool(Constants.GameSetupKeys.PausedInInventory, !paused, false);
    setup.SetBool(Constants.GameSetupKeys.PausedInInventory, paused, false);
    Check(!main.HasChanges("Game") && !main.SaveChanges(), "Setting Switched Back Saves Nothing");

    // Edits On Several Pages And A Tool Save Together
    EditAndCheck(
        "Mixed",
        "Player",
        () =>
        {
            player.PlayerState.Fullness = 55;
            followers.KelvinState.Health = 66;
            setup.SetString(Constants.GameSetupKeys.EnemyDamage, "LOW", "NORMAL");
            main.ResetCannibalAnger();
        },
        () =>
            Math.Abs(player.PlayerState.Fullness - 55) < 0.01
            && Math.Abs(followers.KelvinState.Health - 66) < 0.01
            && setup.GetString(Constants.GameSetupKeys.EnemyDamage, "") == "LOW"
    );

    // Restore Oldest Brings Back The Sample Byte For Byte
    var oldest = BackupManager.List(savegamePath)[^1];
    BackupManager.Restore(savegamePath, oldest.Path);
    Check(SameHashes(pristine, HashFiles(savegamePath)), "Restore Oldest Brings Every File Back");

    // A Missing Backup Fails Before Any File Is Touched
    Check(
        Throws(() => BackupManager.Restore(savegamePath, Path.Combine(work, "Missing")))
            && SameHashes(pristine, HashFiles(savegamePath)),
        "Missing Backup Changes Nothing"
    );

    // A Failed Copy Leaves No Backup Behind
    var archivePath = Path.Combine(savegamePath, "SaveData.zip");
    var backupCount = BackupManager.List(savegamePath).Count;
    using (File.Open(archivePath, FileMode.Open, FileAccess.Read, FileShare.None))
    {
        Check(Throws(() => BackupManager.Create(savegamePath)), "Locked File Fails The Backup");
    }

    Check(BackupManager.List(savegamePath).Count == backupCount, "Failed Backup Leaves No Folder");

    // A File Rewritten In Place Counts As An Outside Change
    main.ReloadSavegame();
    var store = main.SelectedSavegame!.SavegameStore;
    Check(!store.HasChanged(), "Fresh Load Shows No Outside Change");
    File.SetLastWriteTimeUtc(archivePath, File.GetLastWriteTimeUtc(archivePath).AddMinutes(1));
    Check(store.HasChanged(), "Rewritten File Shows As Outside Change");

    // A Missing File Fails The Save And Keeps The Edit
    player.PlayerState.Fullness = 10;
    var movedPath = Path.Combine(work, "Moved.zip");
    File.Move(archivePath, movedPath);
    Check(Throws(() => main.SaveChanges()), "Missing File Fails The Save");
    Check(main.HasUnsavedChanges(), "Failed Save Keeps The Edit");
    File.Move(movedPath, archivePath);

    void EditAndCheck(string page, string route, Action edit, Func<bool> readBack)
    {
        edit();
        Check(main.HasChanges(route), $"{page} Edit Shows As Changed");
        Check(main.SaveChanges(), $"{page} Edit Saves");
        Check(!main.HasChanges(route), $"{page} Shows No Changes After Saving");
        Check(readBack(), $"{page} Edit Reads Back After Reload");
        Check(!main.SaveChanges(), $"{page} Leaves Nothing Unsaved");
    }
}
finally
{
    // The Test Leaves No Settings, Backups Or Copies Behind
    if (settingsBefore != null)
    {
        File.WriteAllBytes(settingsPath, settingsBefore);
    }
    else if (File.Exists(settingsPath))
    {
        File.Delete(settingsPath);
    }

    var backups = Path.Combine(BackupManager.BackupDirectory, SampleSteamId);
    if (Directory.Exists(backups))
    {
        Directory.Delete(backups, true);
    }

    Directory.Delete(work, true);
}

Console.WriteLine(failures.Count == 0 ? "All Checks Passed" : $"{failures.Count} Checks Failed");
return failures.Count == 0 ? 0 : 1;

void Check(bool passed, string name)
{
    Console.WriteLine($"{(passed ? "Passed" : "Failed")} {name}");
    if (!passed)
    {
        failures.Add(name);
    }
}

static bool Throws(Action action)
{
    try
    {
        action();
        return false;
    }
    catch (IOException)
    {
        return true;
    }
}

static GenericSetting SettingOf(IEnumerable<GenericSetting> settings, string name)
{
    return settings.First(setting => setting.Name == name);
}

static (int Collection, int Storage) FirstFillableStorage(StoragePageViewModel storage)
{
    for (var collection = 0; collection < storage.StorageCollections.Count; collection++)
    {
        var storages = storage.StorageCollections[collection].Storages;
        for (var index = 0; index < storages.Count; index++)
        {
            if (storages[index].SupportedItems.Count > 0)
            {
                return (collection, index);
            }
        }
    }

    throw new InvalidOperationException("Found No Storage To Fill");
}

static string SourceDirectory([CallerFilePath] string path = "")
{
    return Path.GetDirectoryName(path)!;
}

static void CopyDirectory(string from, string to)
{
    Directory.CreateDirectory(to);
    foreach (var file in Directory.GetFiles(from))
    {
        File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
    }

    foreach (var directory in Directory.GetDirectories(from))
    {
        CopyDirectory(directory, Path.Combine(to, Path.GetFileName(directory)));
    }
}

static Dictionary<string, string> HashFiles(string directory)
{
    return Directory
        .GetFiles(directory)
        .ToDictionary(
            file => Path.GetFileName(file),
            file => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))
        );
}

static bool SameHashes(Dictionary<string, string> left, Dictionary<string, string> right)
{
    return left.Count == right.Count
        && left.All(pair => right.TryGetValue(pair.Key, out var value) && value == pair.Value);
}
