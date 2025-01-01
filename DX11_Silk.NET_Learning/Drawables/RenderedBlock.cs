using System.Numerics;
using StbImageSharp;

namespace DX11_Silk.NET_Learning.Drawables;

public class RenderedBlock : Drawable.DrawableBase<RenderedBlock>
{
    public override void Update(double deltaTime)
    {
        byte[] imageBytes = File.ReadAllBytes("Resources/Textures/brick.png");
        ImageResult image = ImageResult.FromMemory(imageBytes, ColorComponents.RedGreenBlueAlpha);
    }

    public override Matrix4x4 GetTransform()
    {
        throw new NotImplementedException();
    }
}