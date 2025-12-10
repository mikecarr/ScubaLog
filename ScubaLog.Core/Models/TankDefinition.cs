namespace ScubaLog.Core.Models;

public class TankDefinition
{
    public string Name { get; set; } = "";  // ZTANK.ZNAME
    public double SizeLiters { get; set; }  // ZSIZE
    public double WorkingPressurePsi { get; set; } // ZWORKINGPRESSURE
    public string Type { get; set; } = "";  // ZTYPE
}
