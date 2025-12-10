namespace ScubaLog.Core.Models;

public class GasMix
{
    public string Name { get; set; } = "";  // ZNAME (e.g. EAN33)
    public double O2Percent { get; set; }   // ZOXYGEN (0–1 or 0–100 depending how MacDive stores it)
    public double HePercent { get; set; }   // ZHELIUM
    public double? MinPpo2 { get; set; }    // ZMINPPO2
    public double? MaxPpo2 { get; set; }    // ZMAXPPO2
}