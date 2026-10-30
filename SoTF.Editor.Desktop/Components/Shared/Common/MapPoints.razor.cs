using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models.Map;
using SoTF.Editor.Services.Map;
using SoTF.Editor.Services.Pages;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class MapPoints
{
    // The Look Every Icon Has Unless Its Style Says Otherwise
    private const int DefaultSize = 32;

    // A 3 Pixel Ring Over The 1 Pixel Rim
    private const int RingRoom = 4;

    // Only A New Version Renders, Clicks Leave The Icons Alone
    [Parameter]
    public int Version { get; set; }

    [Inject]
    public required MapViewModel MapViewModel { get; set; }

    [Inject]
    public required PlayerPageViewModel PlayerPage { get; set; }

    [Inject]
    public required IJSRuntime JavaScript { get; set; }

    // JS Draws The Icons, Each Version Sent Once
    private ElementReference _layer;
    private int? _sentVersion;

    private sealed record PointData(
        float X,
        float Y,
        string Icon,
        string Title,
        string Style,
        bool Solo
    );

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Blazor Diffs Thousands Slowly, JS Draws The Near Ones
        if (_sentVersion == Version)
        {
            return;
        }

        _sentVersion = Version;
        var points = MapViewModel
            .VisiblePois()
            .Select(poi => new PointData(
                poi.MapLeft,
                poi.MapTop,
                poi.Icon,
                poi.Title,
                PoiStyle(poi.MapLeft, poi.MapTop, poi.IconSize, poi.IconZIndex),
                poi is PlayerPoi or ZipPointPoi
            ))
            .ToList();
        await JavaScript.InvokeVoidAsync("sotfMap.points", _layer, points);
    }

    public static string PoiStyle(float left, float top, int size, int zIndex)
    {
        // Most Icons Take The Stylesheet's Size And Stacking
        var style = new StringBuilder();
        style.Append(CultureInfo.InvariantCulture, $"--x:{left:0.#};--y:{top:0.#}");
        if (size != DefaultSize)
        {
            style.Append(CultureInfo.InvariantCulture, $";width:{size}px;height:{size}px");
        }

        if (zIndex != 0)
        {
            style.Append(CultureInfo.InvariantCulture, $";z-index:{zIndex + 100}");
        }

        return style.ToString();
    }

    private string HeadingStyle(float left, float top, int zIndex)
    {
        // The Cone Turns Where The Player Looked When Saving
        var angle = CoordinateConverter.MapAngle(PlayerPage.PlayerState.Heading);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"left:{left:0.#}px;top:{top:0.#}px;z-index:{zIndex + 100};--heading:{angle:0.#}deg"
        );
    }

    public static string RingStyle(float left, float top, int size, int zIndex)
    {
        // The Ring Hugs Its Icon
        return PoiStyle(left, top, size + RingRoom, zIndex);
    }

    public static string Pixel(float value)
    {
        // SVG Reads A Dot, Never The Windows Decimal Comma
        return value.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
