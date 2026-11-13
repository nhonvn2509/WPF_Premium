namespace WPF_Premium.Controls;

/// <summary>A geographic position to show on an <see cref="OfflineMapControl"/>.</summary>
public sealed record OfflineMapLocation(double Latitude, double Longitude, string? Label = null, string? Id = null);
