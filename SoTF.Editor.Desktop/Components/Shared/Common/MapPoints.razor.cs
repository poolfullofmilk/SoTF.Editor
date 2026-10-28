using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components;
using SoTF.Editor.Data.Models.Map;
using SoTF.Editor.Services.Map;

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

    // Icons On The Page, Each Render Adds One Chunk More
    private HashSet<IPoi> _drawn = new(ReferenceEqualityComparer.Instance);
    private bool _hasMore;
    private bool _isFilling;
    private bool _isDisposed;

    private List<IPoi> PointsToDraw()
    {
        var drawn = new HashSet<IPoi>(ReferenceEqualityComparer.Instance);
        var points = new List<IPoi>();
        var added = 0;
        _hasMore = false;
        foreach (var poi in MapViewModel.VisiblePois())
        {
            if (_drawn.Contains(poi) || added++ < Chunk)
            {
                drawn.Add(poi);
                points.Add(poi);
            }
            else
            {
                _hasMore = true;
            }
        }

        _drawn = drawn;
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

    public static string PoiStyle(float left, float top, int size, float rotation, int zIndex)
    {
        // Most Icons Take The Stylesheet's Size, Stacking And Angle
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

        if (rotation != 0)
        {
            style.Append(CultureInfo.InvariantCulture, $";--rotation:{rotation:0.#}deg");
        }

        return style.ToString();
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
