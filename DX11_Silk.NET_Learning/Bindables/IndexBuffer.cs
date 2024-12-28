using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;

namespace DX11_Silk.NET_Learning.Bindables;

public class IndexBuffer : IBindable
{
    protected uint indexCount;

    public uint IndexCount => indexCount;
    
    protected readonly ComPtr<ID3D11Buffer> indexBuffer = default;
    
    public unsafe IndexBuffer(ref PeanutGraphics peanutGraphics, ref ushort[] indices)
    {
        indexCount = (uint)indices.Length;
        
        // Creating index buffer data
        BufferDesc bufferDesc = new BufferDesc()
        {
            Usage = Usage.Default,
            ByteWidth = (uint)(indices.Length * sizeof(ushort)),
            MiscFlags = 0u,
            CPUAccessFlags = 0u,
            StructureByteStride = (uint)sizeof(ushort),
            BindFlags = (uint)BindFlag.IndexBuffer,
        };
        fixed (ushort* indexData = indices)
        {
            SubresourceData subresourceData = new SubresourceData
            {
                PSysMem = indexData
            };
            SilkMarshal.ThrowHResult(peanutGraphics.GetDevice.CreateBuffer(
                in bufferDesc, in subresourceData, ref indexBuffer
            ));
        }
    }
    
    public void Bind(ref PeanutGraphics graphics)
    {
        graphics.GetContext.IASetIndexBuffer(indexBuffer, Format.FormatR16Uint, 0u);
    }
}