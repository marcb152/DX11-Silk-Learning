using System.Numerics;

namespace DX11_Silk.NET_Learning.Models;

public struct Vertex
{
    public Vector3 position;
}

public struct ColoredVertex
{
    public Vector3 position;
    public Color color;
    
    public struct Color
    {
        public byte r;
        public byte g;
        public byte b;
        public byte a;
    }
}

public struct TexturedVertex
{
    public Vector3 position;
    public Vector2 texCoord;
}
