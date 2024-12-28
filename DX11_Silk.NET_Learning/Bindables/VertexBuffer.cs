using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;

namespace DX11_Silk.NET_Learning.Bindables;

public class VertexBuffer<T> : IBindable
{
    protected uint stride;
    protected ComPtr<ID3D11Buffer> vertexBuffer = default;
    
    public unsafe VertexBuffer(ref PeanutGraphics graphics, ref T[] vertices)
    {
        stride = (uint)sizeof(T);
        // Creating vertex buffer data
        BufferDesc bufferDesc = new BufferDesc()
        {
            Usage = Usage.Default,
            ByteWidth = (uint)(vertices.Length * sizeof(T)),
            MiscFlags = 0u,
            CPUAccessFlags = 0u,
            StructureByteStride = stride,
            BindFlags = (uint)BindFlag.VertexBuffer,
        };
        fixed (void* vertexData = vertices)
        {
            SubresourceData subresourceData = new SubresourceData
            {
                PSysMem = vertexData
            };
            SilkMarshal.ThrowHResult(graphics.GetDevice.CreateBuffer(in bufferDesc, in subresourceData, ref vertexBuffer));
        }
    }
    
    public void Bind(ref PeanutGraphics graphics)
    {
        graphics.GetContext.IASetVertexBuffers(0u, 1u, ref vertexBuffer, in stride, 0u);
    }
}