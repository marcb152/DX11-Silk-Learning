using System.Diagnostics;
using System.Numerics;
using DX11_Silk.NET_Learning.Bindables;

namespace DX11_Silk.NET_Learning.Drawables;

public abstract partial class Drawable
{
    public abstract class DrawableBase<T> : Drawable
    {
        private static List<IBindable> staticBinds = [];

        protected bool IsStaticInitialized => staticBinds.Count > 0;

        protected static void AddStaticBind(IBindable bind)
        {
            Debug.Assert(bind.GetType() != typeof(IndexBuffer), "Must use AddStaticIndexBuffer to bind index buffer");
            staticBinds.Add(bind);
        }

        protected void AddStaticIndexBuffer(IndexBuffer buffer)
        {
            Debug.Assert(indexBuffer is null, "Attempting to add index buffer a second time");
            indexBuffer = buffer;
            staticBinds.Add(buffer);
        }

        protected void SetIndexFromStatic()
        {
            Debug.Assert(indexBuffer is null, "Attempting to add index buffer a second time");
            foreach (IBindable bind in staticBinds)
            {
                if (bind is IndexBuffer buffer)
                {
                    indexBuffer = buffer;
                    return;
                }
            }
            Debug.Assert(indexBuffer is null, "Failed to find index buffer in static binds");
        }

        public abstract override void Update(double deltaTime);
        public abstract override Matrix4x4 GetTransform();

        protected override List<IBindable> GetStaticBinds()
        {
            return staticBinds;
        }
    }
}