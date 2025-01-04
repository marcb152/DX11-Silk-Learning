using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;

namespace DX11_Silk.NET_Learning.Bindables;

public abstract class ConstantBuffers<T> : IBindable where T : unmanaged
{
    protected ComPtr<ID3D11Buffer> constantBuffer = default;

    protected unsafe ConstantBuffers(ref PeanutGraphics graphics, ref T constants)
    {
        // Create the constant buffer
        BufferDesc bufferDesc = new BufferDesc()
        {
            Usage = Usage.Dynamic,
            ByteWidth = (uint) Marshal.SizeOf<T>(constants),
            MiscFlags = 0u,
            CPUAccessFlags = (uint)CpuAccessFlag.Write,
            StructureByteStride = 0u,
            BindFlags = (uint)BindFlag.ConstantBuffer,
        };
        fixed (T* constBuffer = &constants)
        {
            SubresourceData subresourceData = new SubresourceData
            {
                PSysMem = constBuffer
            };
            SilkMarshal.ThrowHResult(
                graphics.GetDevice.CreateBuffer(in bufferDesc, in subresourceData, ref constantBuffer)
            );
        }
    }
    
    protected unsafe ConstantBuffers(ref PeanutGraphics graphics)
    {
        // Create the constant buffer
        BufferDesc bufferDesc = new BufferDesc()
        {
            Usage = Usage.Dynamic,
            ByteWidth = (uint) sizeof(T),
            MiscFlags = 0u,
            CPUAccessFlags = (uint)CpuAccessFlag.Write,
            StructureByteStride = 0u,
            BindFlags = (uint)BindFlag.ConstantBuffer,
        };
        SilkMarshal.ThrowHResult(
            graphics.GetDevice.CreateBuffer(in bufferDesc, null, ref constantBuffer)
        );
    }
    
    public unsafe void Update(ref PeanutGraphics graphics, ref T data)
    {
        MappedSubresource mappedResource = default;
        graphics.GetContext.Map(constantBuffer, 0u, Map.WriteDiscard, 0u, ref mappedResource);
        Unsafe.CopyBlock(mappedResource.PData, Unsafe.AsPointer(ref data), (uint)sizeof(T));
        graphics.GetContext.Unmap(constantBuffer, 0u);
    }

    public abstract void Bind(ref PeanutGraphics graphics);
}

public class VertexConstantBuffer<T> : ConstantBuffers<T> where T : unmanaged
{
    public VertexConstantBuffer(ref PeanutGraphics graphics, ref T constants) : base(ref graphics, ref constants)
    {
    }

    public VertexConstantBuffer(ref PeanutGraphics graphics) : base(ref graphics)
    {
    }

    public override void Bind(ref PeanutGraphics graphics)
    {
        graphics.GetContext.VSSetConstantBuffers(0u, 1u, ref constantBuffer);
    }
}

public class PixelConstantBuffer<T> : ConstantBuffers<T> where T : unmanaged
{
    public PixelConstantBuffer(ref PeanutGraphics graphics, ref T constants) : base(ref graphics, ref constants)
    {
    }

    public PixelConstantBuffer(ref PeanutGraphics graphics) : base(ref graphics)
    {
    }

    public override void Bind(ref PeanutGraphics graphics)
    {
        graphics.GetContext.PSSetConstantBuffers(0u, 1u, ref constantBuffer);
    }
}