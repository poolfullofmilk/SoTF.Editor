using SoTF.Editor.Data.Models.Map.Static;

namespace SoTF.Editor.Data.Models.Map;

public class DefaultGenericInformationalPoi : InformationalPoi
{
    private readonly string _icon;

    public DefaultGenericInformationalPoi(
        RawPoi rawPoi,
        ItemList itemList,
        string icon,
        HashSet<int> inventoryItems,
        AreaMaskManager areaMaskManager
    )
        : base(
            rawPoi.X,
            rawPoi.Y,
            rawPoi.Teleport?.ToPosition(areaMaskManager),
            rawPoi.Title ?? "",
            rawPoi.Description,
            itemList.GetItems(rawPoi.Requirements),
            inventoryItems,
            rawPoi.IsUnderground,
            rawPoi.Wiki
        )
    {
        _icon = icon;
    }

    public override string Icon => BaseIcon(_icon);
}
