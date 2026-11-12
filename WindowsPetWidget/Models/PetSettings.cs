namespace WindowsPetWidget.Models;

public sealed class PetSettings
{
    public double? Left { get; set; }

    public double? Top { get; set; }

    public bool Topmost { get; set; } = true;

    public PetSize Size { get; set; } = PetSize.Medium;
}

public enum PetSize
{
    Small,
    Medium,
    Large
}
