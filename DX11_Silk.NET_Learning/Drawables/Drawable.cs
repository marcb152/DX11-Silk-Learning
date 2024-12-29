using System.Diagnostics;
using System.Numerics;
using DX11_Silk.NET_Learning.Bindables;

namespace DX11_Silk.NET_Learning.Drawables;

public abstract partial class Drawable
{
    private List<IBindable> bindables = new List<IBindable>();
    private IndexBuffer? indexBuffer;
    
    public void Draw(PeanutGraphics graphics)
    {
        foreach (var bindable in bindables)
        {
            bindable.Bind(ref graphics);
        }
        foreach (IBindable staticBind in GetStaticBinds())
        {
            staticBind.Bind(ref graphics);
        }
        if (indexBuffer is null)
            throw new ArgumentNullException($"{indexBuffer} not set in drawable");
        graphics.DrawIndexed(indexBuffer.IndexCount);
    }
    
    protected void AddBind(IBindable bind)
    {
        Debug.Assert(bind.GetType() != typeof(IndexBuffer), "Must use AddIndexBuffer to bind index buffer");
        bindables.Add(bind);
    }
    
    protected void AddIndexBuffer(IndexBuffer buffer)
    {
        Debug.Assert(indexBuffer is null, "Attempting to add index buffer a second time");
        indexBuffer = buffer;
        bindables.Add(buffer);
    }

    public abstract void Update(double deltaTime);
    
    public abstract Matrix4x4 GetTransform();
    
    protected abstract List<IBindable> GetStaticBinds();
}