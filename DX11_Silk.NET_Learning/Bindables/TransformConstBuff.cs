﻿using System.Numerics;
using DX11_Silk.NET_Learning.Drawables;

namespace DX11_Silk.NET_Learning.Bindables;

public class TransformConstBuff : IBindable
{
    private VertexConstantBuffer<Matrix4x4> vertexConstantBuffer;
    private readonly Drawable parent;
    
    public TransformConstBuff(ref PeanutGraphics graphics, in Drawable parent)
    {
        this.parent = parent;
        vertexConstantBuffer = new VertexConstantBuffer<Matrix4x4>(ref graphics);
    }
    
    public void Bind(ref PeanutGraphics graphics)
    {
        Matrix4x4 transform = Matrix4x4.Transpose(parent.GetTransform() * graphics.ProjectionMatrix);
        vertexConstantBuffer.Update(ref graphics, ref transform);
        vertexConstantBuffer.Bind(ref graphics);
    }
}