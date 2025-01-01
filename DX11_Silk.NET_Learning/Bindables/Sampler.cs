using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;

namespace DX11_Silk.NET_Learning.Bindables;

public class Sampler : IBindable
{
    private ComPtr<ID3D11SamplerState> sampler = default;
    
    public Sampler(ref PeanutGraphics graphics)
    {
        SamplerDesc samplerDesc = new SamplerDesc
        {
            Filter = Filter.MinMagMipPoint,
            AddressU = TextureAddressMode.Wrap,
            AddressV = TextureAddressMode.Wrap,
            AddressW = TextureAddressMode.Wrap
        };
        SilkMarshal.ThrowHResult(
            graphics.GetDevice.CreateSamplerState(in samplerDesc, ref sampler)
        );
    }
    
    public void Bind(ref PeanutGraphics graphics)
    {
        graphics.GetContext.PSSetSamplers(0u, 1u, ref sampler);
    }
}