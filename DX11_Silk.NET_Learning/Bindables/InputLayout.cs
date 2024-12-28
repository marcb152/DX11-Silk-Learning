using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;

namespace DX11_Silk.NET_Learning.Bindables;

public class InputLayout : IBindable
{
    private readonly ComPtr<ID3D11InputLayout> inputLayout = default;
    
    public unsafe InputLayout(
        ref PeanutGraphics peanutGraphics,
        ref ReadOnlySpan<InputElementDesc> layouts,
        ref ComPtr<ID3D10Blob> vertexShaderBytecode)
    {
        SilkMarshal.ThrowHResult(peanutGraphics.GetDevice.CreateInputLayout(
            layouts, (uint)layouts.Length,
            vertexShaderBytecode.GetBufferPointer(), (uint)vertexShaderBytecode.GetBufferSize(),
            inputLayout.GetAddressOf()
        ));
    }
    
    public void Bind(ref PeanutGraphics graphics)
    {
        graphics.GetContext.IASetInputLayout(inputLayout);
    }
}