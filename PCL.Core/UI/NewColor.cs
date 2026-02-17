using System.Numerics;

namespace PCL.Core.UI;

public struct NewColor
{
    private Vector3 _scColor;

    public float ScR
    {
        get => _scColor.X;
        set => _scColor.X = value;
    }
    
    public float ScG
    {
        get => _scColor.Y;
        set => _scColor.Y = value;
    }
    
    public float ScB
    {
        get => _scColor.Z;
        set => _scColor.Z = value;
    }
    
    public float A { get; set; }

    public static NewColor FromXyz(Vector3 xyz)
    {
        var rowR = new Vector3(3.240625f, -1.537208f, -0.498629f);
        var rowG = new Vector3(-0.968931f, 1.875756f, 0.041518f);
        var rowB = new Vector3(0.055710f, -0.204021f, 1.056996f);

        var scR = Vector3.Dot(rowR, xyz);
        var scG = Vector3.Dot(rowG, xyz);
        var scB = Vector3.Dot(rowB, xyz);

        return new NewColor { _scColor = new Vector3(scR, scG, scB) };
    }

    public Vector3 ToXyz()
    {
        var rowX = new Vector3(0.4124f, 0.3576f, 0.1805f);
        var rowY = new Vector3(0.2126f, 0.7152f, 0.0722f);
        var rowZ = new Vector3(0.0193f, 0.1192f, 0.9505f);
        
        var x = Vector3.Dot(rowX, _scColor);
        var y = Vector3.Dot(rowY, _scColor);
        var z = Vector3.Dot(rowZ, _scColor);
        
        return new Vector3(x, y, z);
    }
}