using SoTF.Editor.Services.Actors;
using SoTF.Editor.Services.Map;
using SoTF.Editor.Services.Pages;

namespace SoTF.Editor.Services.Savegames;

public static class EditorServices
{
    // One List For The App And The Round Trip Test
    public static IReadOnlyList<Type> Singletons { get; } =
    [
        typeof(PlayerPageViewModel),
        typeof(ArmorPageViewModel),
        typeof(InventoryPageViewModel),
        typeof(StoragePageViewModel),
        typeof(FollowerPageViewModel),
        typeof(GameSetupPageViewModel),
        typeof(GameStatePageViewModel),
        typeof(WeatherPageViewModel),
        typeof(NpcsPageViewModel),
        typeof(StructuresPageViewModel),
        typeof(MainViewModel),
        typeof(MapViewModel),
        typeof(ActorModifier),
        typeof(MapManager),
        typeof(PoiLoader),
    ];
}
