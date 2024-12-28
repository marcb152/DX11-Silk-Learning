using System.Diagnostics;
using System.Numerics;
using DX11_Silk.NET_Learning.Bindable;

namespace DX11_Silk.NET_Learning;

public abstract class Drawable
{
    private List<IBindable> bindables = new List<IBindable>();
    private IndexBuffer? indexBuffer;
    
    public void Draw(ref PeanutGraphics graphics)
    {
        foreach (var bindable in bindables)
        {
            bindable.Bind(ref graphics);
        }
        if (indexBuffer is null)
            throw new ArgumentNullException($"{indexBuffer} not set in drawable");
        graphics.DrawIndexed(indexBuffer.IndexCount);
    }
    
    public void AddBind(ref IBindable bind)
    {
        Debug.Assert(bind.GetType() != typeof(IndexBuffer), "Must use AddIndexBuffer to bind index buffer");
        bindables.Add(bind);
    }
    
    public void AddIndexBuffer(ref IndexBuffer buffer)
    {
        Debug.Assert(indexBuffer == null, "Attempting to add index buffer a second time");
        indexBuffer = buffer;
        bindables.Add(buffer);
    }

    public abstract void Update(double deltaTime);
    
    public abstract Matrix4x4 GetTransform();
}