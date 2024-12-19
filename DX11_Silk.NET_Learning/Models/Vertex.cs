namespace DX11_Silk.NET_Learning.Models;

public struct Vertex
{
    public float x;
    public float y;
    public float z;
    public Color color;
    
    public struct Color
    {
        public byte r;
        public byte g;
        public byte b;
        public byte a;
    }
}
