using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SoTF.Editor.Data.Helpers;
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

    // Page State
    private ElementReference _viewport;
    private ElementReference _canvas;
    private (float Left, float Top)? _focusPixel;
    private bool _isMapReady;
    private bool _menusHidden;
    private bool _filtersOpen;

    private bool HasFilters =>
        MapViewModel.MapFilter.AreaFilter != AreaFilter.All
        || MapViewModel.MapFilter.RequirementsFilter != MapFilter.RequirementsFilterType.All
        || MapViewModel.MapFilter.ShowOnlyUncollectedItems;

    private string MenusText => AppText.Get(_menusHidden ? "ShowMenus" : "HideMenus");

    protected override void OnInitialized()
    {
        base.OnInitialized();
        MapViewModel.Build();

        if (MapViewModel.FocusPosition is { } focusPosition)
        {
            _focusPixel = CoordinateConverter.IngameToPixel(focusPosition.X, focusPosition.Z);
        }
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
                MapViewModel.FocusPosition = null;
                await JavaScript.InvokeVoidAsync("sotfMap.centerOn", focus.Left, focus.Top);
            }
        }
    }

    private static string PoiStyle(float left, float top, int size, float rotation, int zIndex)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"left:{left:0.#}px;top:{top:0.#}px;width:{size}px;height:{size}px;z-index:{zIndex + 100};--rotation:{rotation:0.#}deg"
        );
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

    private IEnumerable<PoiGroup> ExpandedGroups(PoiGroupCollection collection)
    {
        return IsExpanded(collection) ? collection.PoiGroups.OrderBy(group => group.BaseTitle) : [];
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

    private void SetAllEnabled(bool? enabled)
    {
        MapViewModel.SetAllEnabled(enabled == true);
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
        MapViewModel.MapFilter.AreaFilter = AreaFilter.All;
        MapViewModel.MapFilter.RequirementsFilter = MapFilter.RequirementsFilterType.All;
        MapViewModel.MapFilter.ShowOnlyUncollectedItems = false;
        MapViewModel.ApplyFilterToAllPois();
    }

    private async Task ToggleMenusAsync()
    {
        // The Map Refits While The Menus Slide Away Or Back
        _menusHidden = !_menusHidden;
        await JavaScript.InvokeVoidAsync("sotfMap.fitWhileResizing");
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
        MapViewModel.Release();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
