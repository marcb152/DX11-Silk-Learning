using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;

namespace DX11_Silk.NET_Learning.Bindables;

public class Topology(D3DPrimitiveTopology topology) : IBindable
{
    public void Bind(ref PeanutGraphics graphics)
    {
        graphics.GetContext.IASetPrimitiveTopology(topology);
    }
}