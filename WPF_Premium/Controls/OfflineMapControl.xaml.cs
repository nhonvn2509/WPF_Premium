using System.Collections.Specialized;
using System.IO.Compression;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using BruTile.MbTiles;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Nts.Providers.Shapefile;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;
using Mapsui.Tiling.Layers;
using SQLite;

namespace WPF_Premium.Controls;

/// <summary>Displays a fully offline world map or a local MBTiles map and caller-provided positions.</summary>
public partial class OfflineMapControl : UserControl, IDisposable
{
    private const double DuplicateLabelDistanceDegrees = 0.25;
    private const double CityMajorMaxVisible = 0.02;
    private const double CityMinorMaxVisible = 0.004;
    private readonly Mapsui.UI.Wpf.MapControl _mapControl;
    private readonly List<ShapeFile> _shapeFiles = [];
    private MemoryLayer? _locationsLayer;
    private INotifyCollectionChanged? _notifyingLocations;
    private bool _usesWebMercator;
    private bool _isDisposed;

    public OfflineMapControl()
    {
        InitializeComponent();
        _mapControl = new Mapsui.UI.Wpf.MapControl();
        MapHost.Children.Add(_mapControl);
        _mapControl.MapPointerMoved += MapControl_MapPointerMoved;
        _mapControl.MouseLeave += (_, _) => CoordinateText.Text = "—";
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
            var map = new Map();
            var path = MapFilePath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                MarineLegend.Visibility = Visibility.Collapsed;
                if (!File.Exists(path)) throw new FileNotFoundException("Không tìm thấy tệp MBTiles ngoại tuyến.", path);
                _usesWebMercator = true;
                map.CRS = "EPSG:3857";
                var source = new MbTilesTileSource(new SQLiteConnectionString(Path.GetFullPath(path), storeDateTimeAsTicks: true));
                map.Layers.Add(new TileLayer(source) { Name = "Bản đồ ngoại tuyến" });
            }
            else
            {
                MarineLegend.Visibility = Visibility.Visible;
                _usesWebMercator = false;
                map.CRS = "EPSG:4326";
                map.BackColor = new Color(214, 234, 248, 255);
                BuildWorldMap(map);
            }

            OfflineStatusText.Visibility = Visibility.Collapsed;
            _locationsLayer = new MemoryLayer { Name = "Vị trí" };
            map.Layers.Add(_locationsLayer);
            _mapControl.Map = map;
            if (!_usesWebMercator)
            {
                map.Navigator.ZoomToBox(new MRect(-180, -90, 180, 90));
            }
            UpdateLocations();
        }
        catch (Exception ex)
        {
            MarineLegend.Visibility = Visibility.Collapsed;
            DisposeShapeFiles();
            _mapControl.Map = new Map();
            _locationsLayer = new MemoryLayer { Name = "Vị trí" };
            _mapControl.Map.Layers.Add(_locationsLayer);
            OfflineStatusText.Text = $"Không thể mở bản đồ ngoại tuyến: {ex.Message}";
            OfflineStatusText.Visibility = Visibility.Visible;
            UpdateLocations();
        }
    }

    private void BuildWorldMap(Map map)
    {
        foreach (var band in BathymetryBands)
        {
            map.Layers.Add(new Layer($"Độ sâu đáy biển {band.Depth:N0} m")
            {
                DataSource = CreateShapeFile("bathymetry", $"ne_10m_bathymetry_{band.Suffix}", "ne_10m_bathymetry_all"),
                Style = new VectorStyle
                {
                    Fill = new Brush(band.Color),
                    Outline = new Pen(new Color(65, 123, 151, 110), 0.25)
                }
            });
        }

        var seasSource = CreateShapeFile("seas", "ne_10m_geography_marine_polys");
        map.Layers.Add(new Layer("Biển và đại dương")
        {
            DataSource = seasSource,
            Style = MarineStyle
        });

        var reefsSource = CreateShapeFile("reefs", "ne_10m_reefs");
        map.Layers.Add(new Layer("Rạn san hô và đảo san hô")
        {
            DataSource = reefsSource,
            Style = ReefStyle
        });

        var countriesSource = CreateShapeFile("countries", "ne_10m_admin_0_countries");
        map.Layers.Add(new Layer("Quốc gia")
        {
            DataSource = countriesSource,
            Style = new ThemeStyle(GetCountryStyle)
        });

        var islandsSource = CreateShapeFile("islands", "ne_10m_minor_islands");
        map.Layers.Add(new Layer("Đảo nhỏ")
        {
            DataSource = islandsSource,
            Style = DefaultCountryStyle
        });

        var islandCoastlineSource = CreateShapeFile("islands", "ne_10m_minor_islands_coastline");
        map.Layers.Add(new Layer("Đường bờ đảo nhỏ")
        {
            DataSource = islandCoastlineSource,
            Style = MinorIslandCoastlineStyle,
            MaxVisible = 0.25
        });

        var regionsSource = CreateShapeFile("regions", "ne_10m_admin_1_states_provinces");
        map.Layers.Add(new Layer("Tỉnh và vùng")
        {
            DataSource = regionsSource,
            Style = RegionStyle,
            MaxVisible = 0.3
        });

        map.Layers.Add(BuildIslandLabelsLayer());

        var citiesSource = CreateShapeFile("cities", "ne_10m_populated_places");
        var geographySource = CreateShapeFile("geography", "ne_10m_geography_regions_points");

        map.Layers.Add(BuildLabelsLayer(countriesSource, regionsSource, citiesSource, seasSource, geographySource));
    }

    private void MapControl_MapPointerMoved(object? sender, MapEventArgs e)
    {
        var (lon, lat) = _usesWebMercator
            ? SphericalMercator.ToLonLat(e.WorldPosition.X, e.WorldPosition.Y)
            : (e.WorldPosition.X, e.WorldPosition.Y);

        if (!double.IsFinite(lat) || !double.IsFinite(lon)) return;
        if (lat is < -90 or > 90 || lon is < -180 or > 180) return;

        CoordinateText.Text = string.Format(
            CultureInfo.InvariantCulture,
            "{0:0.0000}°{1}, {2:0.0000}°{3}",
            Math.Abs(lat), lat >= 0 ? "N" : "S",
            Math.Abs(lon), lon >= 0 ? "E" : "W");
    }

    private void UpdateLocations(bool fitToLocations = false)
    {
        if (_locationsLayer is null) return;
        var locations = Locations?.Where(IsValid).ToArray() ?? [];
        var features = new List<IFeature>(locations.Length);
        foreach (var location in locations)
        {
            var (x, y) = ToMapCoordinates(location.Longitude, location.Latitude);
            var feature = new PointFeature(x, y);
            feature["Id"] = location.Id ?? string.Empty;
            feature["Label"] = location.Label ?? string.Empty;
            feature.Styles.Add(new SymbolStyle
            {
                SymbolType = SymbolType.Ellipse,
                SymbolScale = 0.25,
                Fill = new Brush(new Color(36, 130, 210, 255)),
                Outline = null
            });
            if (!string.IsNullOrWhiteSpace(location.Label))
            {
                feature.Styles.Add(new LabelStyle
                {
                    Text = location.Label,
                    ForeColor = new Color(32, 35, 40, 255),
                    BackColor = null,
                    BorderThickness = 0,
                    Offset = new Offset(0, -18),
                    CollisionDetection = true
                });
            }
            features.Add(feature);
        }

        _locationsLayer.Features = features;
        _locationsLayer.DataHasChanged();
        LocationCountText.Text = $"{locations.Length} vị trí";
        if (!fitToLocations || locations.Length == 0) return;
        if (locations.Length == 1)
        {
            var (x, y) = ToMapCoordinates(locations[0].Longitude, locations[0].Latitude);
            _mapControl.Map?.Navigator.CenterOnAndZoomTo(new MPoint(x, y), _usesWebMercator ? 4000 : 0.04);
        }
        else
        {
            var points = locations.Select(location =>
            {
                var (x, y) = ToMapCoordinates(location.Longitude, location.Latitude);
                return new MPoint(x, y);
            }).ToArray();
            var minX = points.Min(p => p.X);
            var minY = points.Min(p => p.Y);
            var maxX = points.Max(p => p.X);
            var maxY = points.Max(p => p.Y);
            var minimumWidth = _usesWebMercator ? 100 : 0.1;
            var minimumHeight = _usesWebMercator ? 100 : 0.05;
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
            var bounds = new MRect(minX, minY, maxX, maxY);
            _mapControl.Map?.Navigator.ZoomToBox(bounds);
        }
    }

    private static bool IsValid(OfflineMapLocation location) =>
        location is not null && double.IsFinite(location.Latitude) && double.IsFinite(location.Longitude) &&
        location.Latitude is >= -90 and <= 90 && location.Longitude is >= -180 and <= 180;

    private (double X, double Y) ToMapCoordinates(double longitude, double latitude) =>
        _usesWebMercator ? SphericalMercator.FromLonLat(longitude, latitude) : (longitude, latitude);

    private static readonly VectorStyle MarineStyle = new()
    {
        Fill = null,
        Outline = new Pen(new Color(103, 163, 194, 150), 0.35)
    };

    private static readonly VectorStyle ReefStyle = new()
    {
        Fill = new Brush(new Color(245, 137, 108, 230)),
        Outline = new Pen(new Color(168, 70, 53, 255), 0.4)
    };

    private static readonly VectorStyle MinorIslandCoastlineStyle = new()
    {
        Fill = null,
        Outline = new Pen(new Color(89, 105, 93, 255), 0.45)
    };

    private static readonly (string Suffix, int Depth, Color Color)[] BathymetryBands =
    [
        ("L_0", 0, new Color(207, 238, 234, 255)),
        ("K_200", 200, new Color(188, 229, 229, 255)),
        ("J_1000", 1000, new Color(165, 217, 224, 255)),
        ("I_2000", 2000, new Color(144, 204, 218, 255)),
        ("H_3000", 3000, new Color(125, 193, 211, 255)),
        ("G_4000", 4000, new Color(108, 181, 203, 255)),
        ("F_5000", 5000, new Color(91, 168, 193, 255)),
        ("E_6000", 6000, new Color(75, 153, 181, 255)),
        ("D_7000", 7000, new Color(60, 137, 167, 255)),
        ("C_8000", 8000, new Color(47, 119, 151, 255)),
        ("B_9000", 9000, new Color(37, 100, 133, 255)),
        ("A_10000", 10000, new Color(29, 78, 111, 255))
    ];

    private static readonly VectorStyle RegionStyle = new()
    {
        Fill = null,
        Outline = new Pen(new Color(120, 130, 120, 255), 0.35)
    };

    private static readonly VectorStyle DefaultCountryStyle = new()
    {
        Fill = new Brush(new Color(226, 232, 210, 255)),
        Outline = new Pen(new Color(120, 130, 120, 255), 0.6)
    };

    private static readonly Dictionary<string, VectorStyle> CountryStyles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Africa"] = new VectorStyle { Fill = new Brush(new Color(233, 228, 200, 255)), Outline = new Pen(new Color(140, 135, 110, 255), 0.6) },
        ["Asia"] = new VectorStyle { Fill = new Brush(new Color(238, 224, 200, 255)), Outline = new Pen(new Color(150, 130, 110, 255), 0.6) },
        ["Europe"] = new VectorStyle { Fill = new Brush(new Color(222, 232, 210, 255)), Outline = new Pen(new Color(120, 140, 120, 255), 0.6) },
        ["North America"] = new VectorStyle { Fill = new Brush(new Color(220, 230, 240, 255)), Outline = new Pen(new Color(120, 135, 155, 255), 0.6) },
        ["South America"] = new VectorStyle { Fill = new Brush(new Color(225, 235, 205, 255)), Outline = new Pen(new Color(125, 145, 115, 255), 0.6) },
        ["Oceania"] = new VectorStyle { Fill = new Brush(new Color(230, 225, 240, 255)), Outline = new Pen(new Color(135, 125, 155, 255), 0.6) },
        ["Antarctica"] = new VectorStyle { Fill = new Brush(new Color(245, 248, 250, 255)), Outline = new Pen(new Color(170, 180, 190, 255), 0.6) },
        ["Seven seas (open ocean)"] = new VectorStyle { Fill = new Brush(new Color(210, 230, 245, 255)), Outline = new Pen(new Color(140, 170, 200, 255), 0.6) }
    };

    private static IStyle? GetCountryStyle(IFeature feature)
    {
        var continent = feature["CONTINENT"]?.ToString() ?? feature["REGION_UN"]?.ToString() ?? string.Empty;
        return CountryStyles.TryGetValue(continent.Trim(), out var style) ? style : DefaultCountryStyle;
    }

    private static readonly LabelStyle CountryLabelMajor = new()
    {
        Font = new Font { FontFamily = "Segoe UI", Size = 12, Bold = true },
        ForeColor = new Color(31, 41, 55, 255),
        BackColor = null,
        BorderThickness = 0,
        Offset = new Offset(0, -8),
        CollisionDetection = true,
        LabelColumn = "Label"
    };

    private static readonly LabelStyle CountryLabelMinor = new()
    {
        Font = new Font { FontFamily = "Segoe UI", Size = 10 },
        ForeColor = new Color(55, 65, 81, 255),
        BackColor = null,
        BorderThickness = 0,
        Offset = new Offset(0, -6),
        CollisionDetection = true,
        MaxVisible = 0.16,
        LabelColumn = "Label"
    };

    private static readonly LabelStyle RegionLabelMajor = new()
    {
        Font = new Font { FontFamily = "Segoe UI", Size = 9, Italic = true },
        ForeColor = new Color(75, 85, 99, 255),
        BackColor = null,
        BorderThickness = 0,
        Offset = new Offset(0, -6),
        CollisionDetection = true,
        MaxVisible = 0.04,
        LabelColumn = "Label"
    };

    private static readonly LabelStyle RegionLabelMinor = new()
    {
        Font = new Font { FontFamily = "Segoe UI", Size = 8, Italic = true },
        ForeColor = new Color(107, 114, 128, 255),
        BackColor = null,
        BorderThickness = 0,
        Offset = new Offset(0, -5),
        CollisionDetection = true,
        MinVisible = 0.001,
        MaxVisible = 0.008,
        LabelColumn = "Label"
    };

    private static readonly LabelStyle RegionLabelDuplicateCityMajor = new()
    {
        Font = new Font { FontFamily = "Segoe UI", Size = 8, Italic = true },
        ForeColor = new Color(107, 114, 128, 255),
        BackColor = null,
        BorderThickness = 0,
        Offset = new Offset(0, -5),
        CollisionDetection = true,
        MinVisible = CityMajorMaxVisible,
        MaxVisible = 0.16,
        LabelColumn = "Label"
    };

    private static readonly LabelStyle RegionLabelDuplicateCityMinor = new()
    {
        Font = new Font { FontFamily = "Segoe UI", Size = 8, Italic = true },
        ForeColor = new Color(107, 114, 128, 255),
        BackColor = null,
        BorderThickness = 0,
        Offset = new Offset(0, -5),
        CollisionDetection = true,
        MinVisible = CityMinorMaxVisible,
        MaxVisible = 0.16,
        LabelColumn = "Label"
    };

    private static readonly LabelStyle CityLabelMajor = new()
    {
        Font = new Font { FontFamily = "Segoe UI", Size = 11, Bold = true },
        ForeColor = new Color(17, 24, 39, 255),
        BackColor = null,
        BorderThickness = 0,
        Offset = new Offset(0, -10),
        CollisionDetection = true,
        MaxVisible = CityMajorMaxVisible,
        LabelColumn = "Label"
    };

    private static readonly LabelStyle CityLabelMinor = new()
    {
        Font = new Font { FontFamily = "Segoe UI", Size = 9 },
        ForeColor = new Color(31, 41, 55, 255),
        BackColor = null,
        BorderThickness = 0,
        Offset = new Offset(0, -8),
        CollisionDetection = true,
        MaxVisible = CityMinorMaxVisible,
        LabelColumn = "Label"
    };

    private static readonly LabelStyle SeaLabelMajor = new()
    {
        Font = new Font { FontFamily = "Segoe UI", Size = 12, Italic = true },
        ForeColor = new Color(29, 78, 216, 255),
        BackColor = null,
        BorderThickness = 0,
        Offset = new Offset(0, -6),
        CollisionDetection = true,
        MaxVisible = 0.08,
        LabelColumn = "Label"
    };

    private static readonly LabelStyle SeaLabelMinor = new()
    {
        Font = new Font { FontFamily = "Segoe UI", Size = 10, Italic = true },
        ForeColor = new Color(37, 99, 235, 255),
        BackColor = null,
        BorderThickness = 0,
        Offset = new Offset(0, -5),
        CollisionDetection = true,
        MinVisible = 0.001,
        MaxVisible = 0.015,
        LabelColumn = "Label"
    };

    private static readonly LabelStyle GeographyLabel = new()
    {
        Font = new Font { FontFamily = "Segoe UI", Size = 14, Bold = true },
        ForeColor = new Color(55, 65, 81, 220),
        BackColor = null,
        BorderThickness = 0,
        Offset = new Offset(0, -6),
        CollisionDetection = true,
        MaxVisible = 0.06,
        LabelColumn = "Label"
    };

    private static readonly LabelStyle GeographyLabelMinor = new()
    {
        Font = new Font { FontFamily = "Segoe UI", Size = 11 },
        ForeColor = new Color(75, 85, 99, 200),
        BackColor = null,
        BorderThickness = 0,
        Offset = new Offset(0, -5),
        CollisionDetection = true,
        MinVisible = 0.001,
        MaxVisible = 0.008,
        LabelColumn = "Label"
    };

    private static readonly LabelStyle IslandLabel = new()
    {
        Font = new Font { FontFamily = "Segoe UI", Size = 9, Italic = true },
        ForeColor = new Color(31, 78, 121, 255),
        BackColor = null,
        BorderThickness = 0,
        Offset = new Offset(0, -5),
        CollisionDetection = true,
        MaxVisible = 0.003,
        LabelColumn = "Label"
    };

    private static readonly LabelStyle MarineFeatureLabel = new()
    {
        Font = new Font { FontFamily = "Segoe UI", Size = 8, Italic = true },
        ForeColor = new Color(37, 99, 235, 255),
        BackColor = null,
        BorderThickness = 0,
        Offset = new Offset(0, -5),
        CollisionDetection = true,
        MaxVisible = 0.0015,
        LabelColumn = "Label"
    };

    private static MemoryLayer BuildIslandLabelsLayer()
    {
        var features = new List<IFeature>();
        AddIslandLabels(features);
        return new MemoryLayer("Nhãn đảo và địa hình biển")
        {
            Style = null,
            Features = features,
            MaxVisible = 0.08
        };
    }

    private static MemoryLayer BuildLabelsLayer(ShapeFile countries, ShapeFile regions, ShapeFile cities, ShapeFile seas, ShapeFile geography)
    {
        var features = new List<IFeature>();
        var cityFeatures = ReadFeatures(cities).ToArray();
        var cityLabelPositions = BuildCityLabelPositions(cityFeatures);
        AddSeaLabels(features, seas);
        AddGeographyLabels(features, geography);
        AddCountryLabels(features, countries);
        AddRegionLabels(features, regions, cityLabelPositions);
        AddCityLabels(features, cityFeatures);
        return new MemoryLayer("Nhãn bản đồ") { Style = null, Features = features };
    }

    private static void AddIslandLabels(ICollection<IFeature> target)
    {
        const string resourceName = "WPF_Premium.Assets.WorldMap.islands.geonames_marine_features.tsv";
        using var resource = typeof(OfflineMapControl).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Thiếu dữ liệu tên đảo tích hợp: {resourceName}");
        using var reader = new StreamReader(resource, Encoding.UTF8, true);
        if (reader.ReadLine() != "Name\tLatitude\tLongitude\tFeatureCode")
            throw new InvalidDataException("Tiêu đề dữ liệu tên đảo không hợp lệ.");

        var lineNumber = 1;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            var (label, latitude, longitude, featureCode) = ParseMarineLabelLine(line, lineNumber);
            var point = new PointFeature(longitude, latitude);
            point["Label"] = label;
            point.Styles.Add(featureCode.StartsWith("T.", StringComparison.Ordinal) ? IslandLabel : MarineFeatureLabel);
            target.Add(point);
        }
    }

    internal static (string Label, double Latitude, double Longitude, string FeatureCode) ParseMarineLabelLine(string line, int lineNumber)
    {
        var fields = line.Split('\t');
        if (fields.Length != 4 ||
            string.IsNullOrWhiteSpace(fields[0]) ||
            string.IsNullOrWhiteSpace(fields[3]) ||
            !double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
            !double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude) ||
            latitude is < -90 or > 90 ||
            longitude is < -180 or > 180)
        {
            throw new InvalidDataException($"Dữ liệu tên đảo không hợp lệ ở dòng {lineNumber}.");
        }

        return (fields[0], latitude, longitude, fields[3]);
    }

    private static void AddSeaLabels(ICollection<IFeature> target, ShapeFile source)
    {
        foreach (var feature in ReadFeatures(source))
        {
            if (feature is not GeometryFeature geometryFeature) continue;
            var label = GetDisplayName(feature, "name_vi", "name");
            if (string.IsNullOrWhiteSpace(label)) continue;
            var rank = ToInt(feature["scalerank"], 5);
            var style = rank <= 3 ? SeaLabelMajor : SeaLabelMinor;
            var point = CreateLabelPoint(geometryFeature, label, style);
            if (point is not null) target.Add(point);
        }
    }

    private static void AddGeographyLabels(ICollection<IFeature> target, ShapeFile source)
    {
        foreach (var feature in ReadFeatures(source))
        {
            if (feature is not GeometryFeature geometryFeature) continue;
            var label = GetDisplayName(feature, "name_vi", "name");
            if (string.IsNullOrWhiteSpace(label)) continue;
            var rank = ToInt(feature["scalerank"], 5);
            var style = rank <= 3 ? GeographyLabel : GeographyLabelMinor;
            var point = CreateLabelPoint(geometryFeature, label, style, "long_x", "lat_y");
            if (point is not null) target.Add(point);
        }
    }

    private static void AddCountryLabels(ICollection<IFeature> target, ShapeFile source)
    {
        foreach (var feature in ReadFeatures(source))
        {
            if (feature is not GeometryFeature geometryFeature) continue;
            var label = GetDisplayName(feature, "NAME_VI", "NAME");
            if (string.IsNullOrWhiteSpace(label)) continue;
            var rank = ToInt(feature["LABELRANK"], 5);
            var style = rank <= 2 ? CountryLabelMajor : CountryLabelMinor;
            var point = CreateLabelPoint(geometryFeature, label, style, "LABEL_X", "LABEL_Y");
            if (point is not null) target.Add(point);
        }
    }

    private static void AddRegionLabels(
        ICollection<IFeature> target,
        ShapeFile source,
        IReadOnlyDictionary<(string Country, string Label), List<(double X, double Y, double MaxVisible)>> cityLabelPositions)
    {
        foreach (var feature in ReadFeatures(source))
        {
            if (feature is not GeometryFeature geometryFeature) continue;
            var label = GetDisplayName(feature, "name_vi", "name");
            if (string.IsNullOrWhiteSpace(label)) continue;
            var position = GetLabelPosition(geometryFeature, "longitude", "latitude");
            if (position is null) continue;
            var country = NormalizeMapLabel(feature["adm0_a3"]?.ToString() ?? string.Empty);
            var duplicateCityMaxVisible = FindNearbyCityLabelMaxVisible(cityLabelPositions, country, label, position.Value);
            var rank = ToInt(feature["labelrank"], 5);
            var style = duplicateCityMaxVisible switch
            {
                CityMajorMaxVisible => RegionLabelDuplicateCityMajor,
                CityMinorMaxVisible => RegionLabelDuplicateCityMinor,
                _ => rank <= 2 ? RegionLabelMajor : RegionLabelMinor
            };
            target.Add(CreateLabelPoint(position.Value, label, style));
        }
    }

    private static void AddCityLabels(ICollection<IFeature> target, IEnumerable<IFeature> source)
    {
        foreach (var feature in source)
        {
            if (feature is not GeometryFeature geometryFeature) continue;
            var label = GetDisplayName(feature, "NAME_VI", "NAME");
            if (string.IsNullOrWhiteSpace(label)) continue;
            var rank = ToInt(feature["SCALERANK"], 5);
            var major = rank <= 4;
            var labelStyle = major ? CityLabelMajor : CityLabelMinor;
            var point = CreateLabelPoint(geometryFeature, label, labelStyle, "LONGITUDE", "LATITUDE");
            if (point is null) continue;
            target.Add(point);
        }
    }

    private static Dictionary<(string Country, string Label), List<(double X, double Y, double MaxVisible)>> BuildCityLabelPositions(IEnumerable<IFeature> source)
    {
        var positionsByLabel = new Dictionary<(string Country, string Label), List<(double X, double Y, double MaxVisible)>>();
        foreach (var feature in source)
        {
            if (feature is not GeometryFeature geometryFeature) continue;
            var label = GetDisplayName(feature, "NAME_VI", "NAME");
            var country = NormalizeMapLabel(feature["ADM0_A3"]?.ToString() ?? string.Empty);
            var position = GetLabelPosition(geometryFeature, "LONGITUDE", "LATITUDE");
            if (string.IsNullOrWhiteSpace(label) || country.Length == 0 || position is null) continue;

            var key = (country, NormalizeMapLabel(label));
            if (!positionsByLabel.TryGetValue(key, out var positions))
            {
                positions = [];
                positionsByLabel.Add(key, positions);
            }
            var rank = ToInt(feature["SCALERANK"], 5);
            positions.Add((position.Value.X, position.Value.Y, rank <= 4 ? CityMajorMaxVisible : CityMinorMaxVisible));
        }
        return positionsByLabel;
    }

    internal static double? FindNearbyCityLabelMaxVisible(
        IReadOnlyDictionary<(string Country, string Label), List<(double X, double Y, double MaxVisible)>> cityLabelPositions,
        string country,
        string label,
        (double X, double Y) position)
    {
        var key = (country, NormalizeMapLabel(label));
        if (!cityLabelPositions.TryGetValue(key, out var cityPositions)) return null;
        foreach (var city in cityPositions)
        {
            var deltaX = city.X - position.X;
            var deltaY = city.Y - position.Y;
            if (deltaX * deltaX + deltaY * deltaY <= DuplicateLabelDistanceDegrees * DuplicateLabelDistanceDegrees)
            {
                return city.MaxVisible;
            }
        }
        return null;
    }

    private static string NormalizeMapLabel(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            result.Append(character is 'đ' or 'Đ' ? 'D' : char.ToUpperInvariant(character));
        }
        return result.ToString().Normalize(NormalizationForm.FormC).Trim();
    }

    private static IEnumerable<IFeature> ReadFeatures(ShapeFile source)
    {
        var fetchInfo = new FetchInfo(new MSection(new MRect(-180, -90, 180, 90), 0.5), "EPSG:4326");
        return source.GetFeaturesAsync(fetchInfo).GetAwaiter().GetResult();
    }

    private static PointFeature? CreateLabelPoint(GeometryFeature feature, string label, IStyle style, string? xColumn = null, string? yColumn = null)
    {
        var position = GetLabelPosition(feature, xColumn, yColumn);
        if (position is null) return null;
        return CreateLabelPoint(position.Value, label, style);
    }

    private static PointFeature CreateLabelPoint((double X, double Y) position, string label, IStyle style)
    {
        var (x, y) = position;
        var point = new PointFeature(x, y);
        point["Label"] = label;
        point.Styles.Add(style);
        return point;
    }

    private static (double X, double Y)? GetLabelPosition(GeometryFeature feature, string? xColumn, string? yColumn)
    {
        if (xColumn is not null && yColumn is not null)
        {
            var x = ToDouble(feature[xColumn]);
            var y = ToDouble(feature[yColumn]);
            if (double.IsFinite(x) && double.IsFinite(y) && x is >= -180 and <= 180 && y is >= -90 and <= 90)
                return (x, y);
        }
        var centroid = feature.Geometry?.Centroid;
        if (centroid is not null && double.IsFinite(centroid.X) && double.IsFinite(centroid.Y))
            return (centroid.X, centroid.Y);
        return null;
    }

    private static string? GetDisplayName(IFeature feature, string primary, string fallback)
    {
        var value = feature[primary]?.ToString();
        if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        return feature[fallback]?.ToString()?.Trim();
    }

    private static double ToDouble(object? value)
    {
        if (value is null || value is DBNull) return double.NaN;
        if (value is double d) return d;
        if (value is float f) return f;
        if (value is int i) return i;
        if (value is long l) return l;
        return double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : double.NaN;
    }

    private static int ToInt(object? value, int fallback)
    {
        if (value is null || value is DBNull) return fallback;
        if (value is int i) return i;
        if (value is double d) return (int)d;
        if (value is long l) return (int)l;
        return int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : fallback;
    }

    private ShapeFile CreateShapeFile(string folder, string fileName, string? archiveName = null)
    {
        var path = ExtractShapeFile(folder, fileName, archiveName);
        var source = new ShapeFile(path, fileBasedIndex: true, readPrjFile: true)
        {
            Encoding = Encoding.UTF8
        };
        _shapeFiles.Add(source);
        return source;
    }

    private static string ExtractShapeFile(string folder, string fileName, string? archiveName = null)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WPF_Premium", "WorldMap", folder);
        Directory.CreateDirectory(directory);
        var shapePath = Path.Combine(directory, $"{fileName}.shp");
        var extensions = new[] { "shp", "shx", "dbf", "prj", "cpg" };
        if (extensions.All(extension => File.Exists(Path.Combine(directory, $"{fileName}.{extension}"))))
            return shapePath;

        var resourceName = $"WPF_Premium.Assets.WorldMap.{folder}.{archiveName ?? fileName}.zip";
        using var resource = typeof(OfflineMapControl).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Thiếu dữ liệu bản đồ tích hợp: {resourceName}");
        using var archive = new ZipArchive(resource, ZipArchiveMode.Read);
        foreach (var extension in extensions)
        {
            var destination = Path.Combine(directory, $"{fileName}.{extension}");
            var entry = archive.Entries.FirstOrDefault(candidate =>
                string.Equals(Path.GetFileName(candidate.FullName), $"{fileName}.{extension}", StringComparison.OrdinalIgnoreCase));
            if (entry is null) throw new FileNotFoundException($"Thiếu tệp bản đồ {fileName}.{extension} trong {resourceName}");
            using var input = entry.Open();
            using var output = File.Create(destination);
            input.CopyTo(output);
        }
        return shapePath;
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => _mapControl.Map?.Navigator.ZoomIn();
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => _mapControl.Map?.Navigator.ZoomOut();
    private void ZoomToLocations_Click(object sender, RoutedEventArgs e) => UpdateLocations(fitToLocations: true);

    private void DisposeShapeFiles()
    {
        foreach (var shapeFile in _shapeFiles) shapeFile.Dispose();
        _shapeFiles.Clear();
    }

    private void DisposeMap()
    {
        DisposeShapeFiles();
        foreach (var layer in _mapControl.Map.Layers.OfType<IDisposable>()) layer.Dispose();
        _mapControl.Map.Dispose();
        _mapControl.Map = new Map();
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