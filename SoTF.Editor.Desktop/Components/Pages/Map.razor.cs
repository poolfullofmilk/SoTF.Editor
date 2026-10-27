using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using SoTF.Editor.Data.Models.Map;
using SoTF.Editor.Desktop.Components.Shared.Common;
using SoTF.Editor.Services.Map;

namespace SoTF.Editor.Desktop.Components.Pages;

public partial class Map : EditorPageBase
{
    // The Map Image Fades Into This At Its Edges
    private const string MapBackground = "#6186be";

    // Camera, Filters And Selection Edit Nothing, Zipline Edits Notify Themselves
    protected override bool EditsSavegame => false;

    [Inject]
    public required MapViewModel MapViewModel { get; set; }

    [Inject]
    public required IJSRuntime JavaScript { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    // Page State
    private ElementReference _viewport;
    private ElementReference _canvas;
    private (float Left, float Top)? _focusPixel;
    private bool _isMapReady;
    private bool _menusHidden;

    // Says What To Click While A Zipline Waits
    private Snackbar? _pickHint;
    private string? _pickHintKey;

    private bool HasFilters =>
        MapViewModel.MapFilter.AreaFilter != AreaFilter.All
        || MapViewModel.MapFilter.RequirementsFilter != MapFilter.RequirementsFilterType.All
        || MapViewModel.MapFilter.ShowOnlyUncollectedItems;

    private static string? CheckOf(bool selected)
    {
        return selected ? Icons.Material.Rounded.Check : null;
    }

    // Groups The Search Or Filters Leave Empty Go Grey
    private static string? MatchClass(PoiGroup group)
    {
        return MapViewModel.HasMatches(group) ? null : "not-held";
    }

    private static string? MatchClass(IEnumerable<PoiGroup> groups)
    {
        return groups.Any(MapViewModel.HasMatches) ? null : "not-held";
    }

    private string MenusText => AppText.Get(_menusHidden ? "ShowMenus" : "HideMenus");

    protected override void OnInitialized()
    {
        base.OnInitialized();
        MapViewModel.Build();

        // The Point Itself When One Sits There, Its Spot Otherwise
        if (MapViewModel.TakeFocus() is { } focused)
        {
            _focusPixel = (focused.MapLeft, focused.MapTop);
        }
        else if (MapViewModel.FocusPosition is { } focusPosition)
        {
            MapViewModel.FocusPosition = null;
            var spot = new LocationPoi(focusPosition);
            MapViewModel.SelectPoi(spot);
            _focusPixel = (spot.MapLeft, spot.MapTop);
        }
    }

    private async Task ClickMapAsync(MouseEventArgs args)
    {
        // JS Knows The Camera, Right Click Wants Only The Spot
        if (
            await JavaScript.InvokeAsync<float[]?>("sotfMap.toMap", args.ClientX, args.ClientY)
            is [var left, var top, var zoom]
        )
        {
            MapViewModel.Click(left, top, zoom, args.Button == 2);
        }
    }

    private void SyncPickHint()
    {
        var key =
            MapViewModel.PickFrom is null ? null
            : MapViewModel.PicksNewZipline ? "ClickZiplineEnd"
            : "ClickAnchorSpot";
        if (key == _pickHintKey)
        {
            return;
        }

        if (_pickHint is { } hint)
        {
            Snackbar.Remove(hint);
        }

        _pickHint = key is null
            ? null
            : Snackbar.Add(
                AppText.Get(key),
                Severity.Info,
                options => options.RequireInteraction = true
            );
        _pickHintKey = key;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // The Guard Waits Out A Render Without A Savegame
        if (_viewport.Id is null)
        {
            return;
        }

        if (!_isMapReady)
        {
            _isMapReady = true;
            await JavaScript.InvokeVoidAsync("sotfMap.init", _viewport, _canvas);

            // Show On Map Lands Centred On Its Position
            if (_focusPixel is { } focus)
            {
                await JavaScript.InvokeVoidAsync("sotfMap.centerOn", focus.Left, focus.Top);
            }
        }

        SyncPickHint();
    }

    private static string PoiStyle(float left, float top, int size, float rotation, int zIndex)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"left:{left:0.#}px;top:{top:0.#}px;width:{size}px;height:{size}px;z-index:{zIndex + 100};--rotation:{rotation:0.#}deg"
        );
    }

    private static string Pixel(float value)
    {
        // SVG Reads A Dot, Never The Windows Decimal Comma
        return value.ToString("0.#", CultureInfo.InvariantCulture);
    }

    private static string RequirementsName(MapFilter.RequirementsFilterType requirementsFilter)
    {
        return AppText.Get(TextKeys.RequirementsFilter(requirementsFilter));
    }

    // Sub Groups Start Folded, The Checkbox Still Covers All
    private readonly HashSet<string> _expandedCollections = [];

    private bool IsExpanded(PoiGroupCollection collection)
    {
        return _expandedCollections.Contains(collection.GroupKey);
    }

    private void ToggleExpanded(PoiGroupCollection collection)
    {
        if (!_expandedCollections.Remove(collection.GroupKey))
        {
            _expandedCollections.Add(collection.GroupKey);
        }
    }

    // A Group Alone, A Collection Its Groups Once Opened
    private IEnumerable<PoiGroup> GroupsUnder(IPoiGrouper grouper)
    {
        return grouper switch
        {
            PoiGroup group => [group],
            PoiGroupCollection collection when IsExpanded(collection) =>
                collection.PoiGroups.OrderBy(group => group.Title),
            _ => [],
        };
    }

    private static string RowClass(PoiGroup group, IPoiGrouper grouper)
    {
        // Sub Groups Sit Under Their Collection, Empty Ones Go Grey
        return $"{(grouper is PoiGroupCollection ? "ml-6" : "")} {MatchClass(group)}";
    }

    private static bool? StateOf(IEnumerable<PoiGroup> groups)
    {
        // Mixed Groups Show The Third State
        if (groups.All(group => group.Enabled))
        {
            return true;
        }

        return groups.Any(group => group.Enabled) ? null : false;
    }

    private void ToggleAll()
    {
        // A Click Checks Or Clears, Only Picked Groups Show Mixed
        MapViewModel.SetAllEnabled(StateOf(MapViewModel.AllGroups()) != true);
        MapViewModel.ApplyFilterToAllPois();
    }

    private void ResetToDefault()
    {
        MapViewModel.ResetToDefault();
        MapViewModel.ApplyFilterToAllPois();
    }

    private void SetEnabled(IPoiGrouper grouper, bool enabled)
    {
        grouper.Enabled = enabled;
        MapViewModel.ApplyFilterToAllPois();
        MapViewModel.SaveSettings();
    }

    private void SetFullText(string? fullText)
    {
        MapViewModel.MapFilter.FullText = fullText;
        MapViewModel.ApplyFilterToAllPois();
    }

    private void SetAreaFilter(AreaFilter areaFilter)
    {
        MapViewModel.MapFilter.AreaFilter = areaFilter;
        MapViewModel.ApplyFilterToAllPois();
    }

    private void SetRequirementsFilter(MapFilter.RequirementsFilterType requirementsFilter)
    {
        MapViewModel.MapFilter.RequirementsFilter = requirementsFilter;
        MapViewModel.ApplyFilterToAllPois();
    }

    private void SetUncollectedOnly(bool showOnlyUncollectedItems)
    {
        MapViewModel.MapFilter.ShowOnlyUncollectedItems = showOnlyUncollectedItems;
        MapViewModel.ApplyFilterToAllPois();
    }

    private void ResetFilters()
    {
        // The Search Stays, It Has Its Own Clear
        var search = MapViewModel.MapFilter.FullText;
        MapViewModel.MapFilter.Reset();
        MapViewModel.MapFilter.FullText = search;
        MapViewModel.ApplyFilterToAllPois();
    }

    private void ToggleMenus()
    {
        _menusHidden = !_menusHidden;
    }

    private async Task ZoomAsync(double factor)
    {
        await JavaScript.InvokeVoidAsync("sotfMap.zoomBy", factor);
    }

    private async Task CenterAsync()
    {
        await JavaScript.InvokeVoidAsync("sotfMap.reset");
    }

    public override void Dispose()
    {
        // No JS Here, A Closing Window Could Hang On It
        if (_pickHint is { } hint)
        {
            Snackbar.Remove(hint);
        }

        MapViewModel.Release();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
