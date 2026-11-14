using WPF_Premium.Controls;

namespace WPF_Premium.UIAutomationTests;

[TestClass]
public sealed class OfflineMapLabelTests
{
    [TestMethod]
    public void SameNameRegionAndCityLabelsAreMatchedOnlyWhenNearbyInTheSameCountry()
    {
        var cityLabels = new Dictionary<(string Country, string Label), List<(double X, double Y, double MaxVisible)>>
        {
            [("VNM", "HOA BINH")] = [(105.338314, 20.813702, 0.004)]
        };

        Assert.AreEqual(
            0.004,
            OfflineMapControl.FindNearbyCityLabelMaxVisible(cityLabels, "VNM", "Hòa Bình", (105.340, 20.7292)));
        Assert.IsNull(
            OfflineMapControl.FindNearbyCityLabelMaxVisible(cityLabels, "MMR", "Hòa Bình", (105.340, 20.7292)));
        Assert.IsNull(
            OfflineMapControl.FindNearbyCityLabelMaxVisible(cityLabels, "VNM", "Hòa Bình", (106, 20.7292)));
    }

    [TestMethod]
    public void MarineLabelRowsParseWgs84CoordinatesAndRejectInvalidCoordinates()
    {
        var parsed = OfflineMapControl.ParseMarineLabelLine("Đảo A\t10.5\t110.25\tT.ISL", 2);

        Assert.AreEqual("Đảo A", parsed.Label);
        Assert.AreEqual(10.5, parsed.Latitude);
        Assert.AreEqual(110.25, parsed.Longitude);
        Assert.AreEqual("T.ISL", parsed.FeatureCode);
        Assert.ThrowsExactly<System.IO.InvalidDataException>(
            () => OfflineMapControl.ParseMarineLabelLine("Đảo B\t91\t110.25\tT.ISL", 3));
    }

    [TestMethod]
    public void EmbeddedMarineLabelDataContainsValidRows()
    {
        var resourceName = $"{typeof(OfflineMapControl).Assembly.GetName().Name}.Assets.WorldMap.islands.geonames_marine_features.tsv";
        using var resource = typeof(OfflineMapControl).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource: {resourceName}");
        using var reader = new System.IO.StreamReader(resource);
        Assert.AreEqual("Name\tLatitude\tLongitude\tFeatureCode", reader.ReadLine());

        var lineNumber = 1;
        var count = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            _ = OfflineMapControl.ParseMarineLabelLine(line, lineNumber);
            count++;
        }

        Assert.IsGreaterThan(0, count);
    }
}
