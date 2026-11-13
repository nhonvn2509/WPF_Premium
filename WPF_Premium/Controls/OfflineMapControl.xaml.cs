using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using BruTile.MbTiles;
using Mapsui.Layers;
using Mapsui.Nts.Providers.Shapefile;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;
using SQLite;

namespace WPF_Premium.Controls;

/// <summary>Displays a fully offline world map or a local MBTiles map and caller-provided positions.</summary>
public partial class OfflineMapControl : UserControl, IDisposable
{
    private readonly Mapsui.UI.Wpf.MapControl _mapControl;
    private MemoryLayer? _locationsLayer;
    private ShapeFile? _worldMapSource;
    private INotifyCollectionChanged? _notifyingLocations;
    private bool _usesWebMercator;
    private bool _isDisposed;

    public OfflineMapControl()
    {
        InitializeComponent();
        _mapControl = new Mapsui.UI.Wpf.MapControl();
        MapHost.Children.Add(_mapControl);
        Loaded += (_, _) => RebuildMap();
        Unloaded += (_, _) => DisposeMap();
    }

    /// <summary>Optional full path to a local MBTiles file. When empty, the built-in world map is shown.</summary>
    public string? MapFilePath
    {
        get => (string?)GetValue(MapFilePathProperty);
        set => SetValue(MapFilePathProperty, value);
    }

    public static readonly DependencyProperty MapFilePathProperty = DependencyProperty.Register(
        nameof(MapFilePath), typeof(string), typeof(OfflineMapControl), new PropertyMetadata(null, OnMapSettingsChanged));

    /// <summary>WGS84 locations; latitude is -90..90 and longitude is -180..180.</summary>
    public IEnumerable<OfflineMapLocation> Locations
    {
        get => (IEnumerable<OfflineMapLocation>)GetValue(LocationsProperty);
        set => SetValue(LocationsProperty, value);
    }

    public static readonly DependencyProperty LocationsProperty = DependencyProperty.Register(
        nameof(Locations), typeof(IEnumerable<OfflineMapLocation>), typeof(OfflineMapControl),
        new PropertyMetadata(Array.Empty<OfflineMapLocation>(), OnLocationsChanged));

    /// <summary>Optional attribution text for the supplied local map dataset.</summary>
    public string? Attribution
    {
        get => (string?)GetValue(AttributionProperty);
        set => SetValue(AttributionProperty, value);
    }

    public static readonly DependencyProperty AttributionProperty = DependencyProperty.Register(
        nameof(Attribution), typeof(string), typeof(OfflineMapControl), new PropertyMetadata(null));

    private static void OnMapSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((OfflineMapControl)d).RebuildMap();

    private static void OnLocationsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (OfflineMapControl)d;
        if (control._notifyingLocations is not null) control._notifyingLocations.CollectionChanged -= control.Locations_CollectionChanged;
        control._notifyingLocations = e.NewValue as INotifyCollectionChanged;
        if (control._notifyingLocations is not null) control._notifyingLocations.CollectionChanged += control.Locations_CollectionChanged;
        control.UpdateLocations();
    }

    private void Locations_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateLocations();

    private void RebuildMap()
    {
        if (_isDisposed || !IsLoaded) return;
        try
        {
            DisposeMap();
            var map = new Mapsui.Map();
            var path = MapFilePath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                if (!File.Exists(path)) throw new FileNotFoundException("Kh\u00F4ng t\u00ECm th\u1EA5y t\u1EC7p MBTiles ngo\u1EA1i tuy\u1EBFn.", path);
                _usesWebMercator = true;
                map.CRS = "EPSG:3857";
                var source = new MbTilesTileSource(new SQLiteConnectionString(Path.GetFullPath(path), storeDateTimeAsTicks: true));
                map.Layers.Add(new TileLayer(source) { Name = "B\u1EA3n \u0111\u1ED3 ngo\u1EA1i tuy\u1EBFn" });
            }
            else
            {
                _usesWebMercator = false;
                map.CRS = "EPSG:4326";
                map.BackColor = new Mapsui.Styles.Color(225, 238, 247, 255);
                var worldMapPath = ExtractWorldMap();
                _worldMapSource = new ShapeFile(worldMapPath, readPrjFile: true);
                map.Layers.Add(new Layer
                {
                    Name = "B\u1EA3n \u0111\u1ED3 th\u1EBF gi\u1EDBi",
                    DataSource = _worldMapSource,
                    Style = new VectorStyle
                    {
                        Fill = new Mapsui.Styles.Brush(new Mapsui.Styles.Color(221, 231, 214, 255)),
                        Outline = new Pen(new Mapsui.Styles.Color(145, 160, 140, 255), 0.7)
                    }
                });
            }

            OfflineStatusText.Visibility = Visibility.Collapsed;
            _locationsLayer = new MemoryLayer { Name = "V\u1ECB tr\u00ED" };
            map.Layers.Add(_locationsLayer);
            _mapControl.Map = map;
            if (!_usesWebMercator)
            {
                map.Navigator.ZoomToBox(new Mapsui.MRect(-180, -90, 180, 90));
            }
            UpdateLocations();
        }
        catch (Exception ex)
        {
            _mapControl.Map = new Mapsui.Map();
            _locationsLayer = new MemoryLayer { Name = "V\u1ECB tr\u00ED" };
            _mapControl.Map.Layers.Add(_locationsLayer);
            OfflineStatusText.Text = $"Kh\u00F4ng th\u1EC3 m\u1EDF b\u1EA3n \u0111\u1ED3 ngo\u1EA1i tuy\u1EBFn: {ex.Message}";
            OfflineStatusText.Visibility = Visibility.Visible;
            UpdateLocations();
        }
    }

    private void UpdateLocations(bool fitToLocations = false)
    {
        if (_locationsLayer is null) return;
        var locations = Locations?.Where(IsValid).ToArray() ?? [];
        var features = new List<Mapsui.IFeature>(locations.Length);
        foreach (var location in locations)
        {
            var (x, y) = ToMapCoordinates(location.Longitude, location.Latitude);
            var feature = new PointFeature(x, y);
            feature["Id"] = location.Id ?? string.Empty;
            feature["Label"] = location.Label ?? string.Empty;
            feature.Styles.Add(new SymbolStyle
            {
                SymbolType = SymbolType.Ellipse,
                SymbolScale = 0.9,
                Fill = new Mapsui.Styles.Brush(new Mapsui.Styles.Color(36, 130, 210, 255)),
                Outline = new Pen(new Mapsui.Styles.Color(255, 255, 255, 255), 2)
            });
            if (!string.IsNullOrWhiteSpace(location.Label))
            {
                feature.Styles.Add(new LabelStyle
                {
                    Text = location.Label,
                    ForeColor = new Mapsui.Styles.Color(32, 35, 40, 255),
                    BackColor = new Mapsui.Styles.Brush(new Mapsui.Styles.Color(245, 255, 255, 255)),
                    Offset = new Mapsui.Styles.Offset(0, -18),
                    CollisionDetection = true
                });
            }
            features.Add(feature);
        }

        _locationsLayer.Features = features;
        _locationsLayer.DataHasChanged();
        LocationCountText.Text = $"{locations.Length} v\u1ECB tr\u00ED";
        if (!fitToLocations || locations.Length == 0) return;
        if (locations.Length == 1)
        {
            var (x, y) = ToMapCoordinates(locations[0].Longitude, locations[0].Latitude);
            _mapControl.Map?.Navigator.CenterOnAndZoomTo(new Mapsui.MPoint(x, y), _usesWebMercator ? 4000 : 0.04);
        }
        else
        {
            var points = locations.Select(location =>
            {
                var (x, y) = ToMapCoordinates(location.Longitude, location.Latitude);
                return new Mapsui.MPoint(x, y);
            }).ToArray();
            var minX = points.Min(p => p.X);
            var minY = points.Min(p => p.Y);
            var maxX = points.Max(p => p.X);
            var maxY = points.Max(p => p.Y);
            var minimumWidth = _usesWebMercator ? 100 : 30;
            var minimumHeight = _usesWebMercator ? 100 : 15;
            if (maxX - minX < minimumWidth)
            {
                var center = (minX + maxX) / 2;
                minX = center - minimumWidth / 2;
                maxX = center + minimumWidth / 2;
            }
            if (maxY - minY < minimumHeight)
            {
                var center = (minY + maxY) / 2;
                minY = center - minimumHeight / 2;
                maxY = center + minimumHeight / 2;
            }
            var bounds = new Mapsui.MRect(minX, minY, maxX, maxY);
            _mapControl.Map?.Navigator.ZoomToBox(bounds);
        }
    }

    private static bool IsValid(OfflineMapLocation location) =>
        location is not null && double.IsFinite(location.Latitude) && double.IsFinite(location.Longitude) &&
        location.Latitude is >= -90 and <= 90 && location.Longitude is >= -180 and <= 180;

    private (double X, double Y) ToMapCoordinates(double longitude, double latitude) =>
        _usesWebMercator ? SphericalMercator.FromLonLat(longitude, latitude) : (longitude, latitude);

    private static string ExtractWorldMap()
    {
        const string fileName = "ne_110m_admin_0_countries";
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WPF_Premium", "WorldMap");
        Directory.CreateDirectory(directory);
        foreach (var extension in new[] { "shp", "shx", "dbf", "prj", "cpg" })
        {
            var destination = Path.Combine(directory, $"{fileName}.{extension}");
            if (File.Exists(destination)) continue;
            var resourceName = $"WPF_Premium.Assets.WorldMap.{fileName}.{extension}";
            using var resource = typeof(OfflineMapControl).Assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException($"Thi\u1EBFu d\u1EEF li\u1EC7u b\u1EA3n \u0111\u1ED3 t\u00EDch h\u1EE3p: {resourceName}");
            using var output = File.Create(destination);
            resource.CopyTo(output);
        }
        return Path.Combine(directory, $"{fileName}.shp");
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => _mapControl.Map?.Navigator.ZoomIn();
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => _mapControl.Map?.Navigator.ZoomOut();
    private void ZoomToLocations_Click(object sender, RoutedEventArgs e) => UpdateLocations(fitToLocations: true);

    private void DisposeMap()
    {
        _worldMapSource?.Dispose();
        _worldMapSource = null;
        foreach (var layer in _mapControl.Map.Layers.OfType<IDisposable>()) layer.Dispose();
        _mapControl.Map.Dispose();
        _mapControl.Map = new Mapsui.Map();
        _locationsLayer = null;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        if (_notifyingLocations is not null) _notifyingLocations.CollectionChanged -= Locations_CollectionChanged;
        DisposeMap();
    }
}
