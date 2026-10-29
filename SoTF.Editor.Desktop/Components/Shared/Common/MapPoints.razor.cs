using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models.Map;
using SoTF.Editor.Services.Map;
using SoTF.Editor.Services.Pages;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class MapPoints : IDisposable
{
    // New Icons Per Frame, So Thousands Never Block The Page
    private const int Chunk = 1200;

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

    // Icons In Page Order, Each Render Adds One Chunk More
    private List<IPoi> _drawn = [];
    private bool _hasMore;
    private bool _isFilling;
    private bool _isDisposed;

    private List<IPoi> PointsToDraw()
    {
        // Kept Icons Stay Put, New Ones Join The End
        var visible = MapViewModel.VisiblePois().ToList();
        var shown = new HashSet<IPoi>(visible, ReferenceEqualityComparer.Instance);
        var points = _drawn.Where(shown.Contains).ToList();
        var drawn = new HashSet<IPoi>(points, ReferenceEqualityComparer.Instance);
        var added = 0;
        _hasMore = false;
        foreach (var poi in visible.Where(poi => !drawn.Contains(poi)))
        {
            if (added++ == Chunk)
            {
                _hasMore = true;
                break;
            }

            points.Add(poi);
        }

        _drawn = points;
        return points;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // The Next Chunk Waits A Frame, So Clicks Get Through
        if (!_hasMore || _isFilling)
        {
            return;
        }

        _isFilling = true;
        await Task.Delay(8);
        _isFilling = false;
        if (!_isDisposed)
        {
            StateHasChanged();
        }
    }

    public static string PoiStyle(float left, float top, int size, int zIndex)
    {
        // Most Icons Take The Stylesheet's Size And Stacking
        var style = new StringBuilder();
        style.Append(CultureInfo.InvariantCulture, $"left:{left:0.#}px;top:{top:0.#}px");
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
        // The Ring Hugs Its Icon, Most Take The Stylesheet's Size
        var style = new StringBuilder();
        style.Append(
            CultureInfo.InvariantCulture,
            $"left:{left:0.#}px;top:{top:0.#}px;z-index:{zIndex + 100}"
        );
        if (size != DefaultSize)
        {
            style.Append(
                CultureInfo.InvariantCulture,
                $";width:{size + RingRoom}px;height:{size + RingRoom}px"
            );
        }

        return style.ToString();
    }

    public static string Pixel(float value)
    {
        // SVG Reads A Dot, Never The Windows Decimal Comma
        return value.ToString("0.#", CultureInfo.InvariantCulture);
    }

    public void Dispose()
    {
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
