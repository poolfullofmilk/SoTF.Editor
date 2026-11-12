using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.Models.Map;

public sealed class LocationPoi(Position position) : BasePoi(position)
{
    public const string IconFile = "pin.svg";

    // Past This A Borrowed Height Means Little
    public const float FarMetres = 30;

    public override string Icon => BaseIcon(IconFile);

    public override string Title => AppText.Get("Location");

    // Distance To The Point Lending Its Height
    public float? HeightSourceMetres { get; init; }

    public bool HeightIsFarGuess => HeightSourceMetres > FarMetres;
}
