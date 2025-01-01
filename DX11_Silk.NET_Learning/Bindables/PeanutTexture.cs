using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using StbImageSharp;

namespace DX11_Silk.NET_Learning.Bindables;

public class PeanutTexture : IBindable
{
    private ComPtr<ID3D11ShaderResourceView> resourceView = default;
    
    public unsafe PeanutTexture(ref PeanutGraphics graphics, ref ImageResult imageResult)
    {
        // Create texture resource
        Texture2DDesc texture2DDesc = new Texture2DDesc
        {
            Width = (uint)imageResult.Width,
            Height = (uint)imageResult.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.FormatR8G8B8A8Unorm,
            SampleDesc = new SampleDesc(1, 0),
            Usage = Usage.Default,
            BindFlags = (uint)BindFlag.ShaderResource,
            CPUAccessFlags = 0,
            MiscFlags = 0
        };
        
        ComPtr<ID3D11Texture2D> texture = default;
        fixed (byte* data = imageResult.Data)
        {
            SubresourceData subresourceData = new SubresourceData
            {
                PSysMem = data,
                // Comp is the number of color channels, multiplied by sizeof(byte) to get the byte-size of each pixel
                // SysMemPitch is the byte-size of each row of pixels
                SysMemPitch = (uint)(imageResult.Width * (int)imageResult.Comp * sizeof(byte))
            };
            SilkMarshal.ThrowHResult(
                graphics.GetDevice.CreateTexture2D(in texture2DDesc, in subresourceData, ref texture)
            );
        }
        
        // Create the resource view on the texture
        ShaderResourceViewDesc shaderResourceViewDesc = new ShaderResourceViewDesc
        {
            Format = texture2DDesc.Format,
            ViewDimension = D3DSrvDimension.D3D11SrvDimensionTexture2D,
            Texture2D = new Tex2DSrv()
            {
                MipLevels = 1,
                MostDetailedMip = 0
            }
        };
        SilkMarshal.ThrowHResult(
            graphics.GetDevice.CreateShaderResourceView(texture, in shaderResourceViewDesc, ref resourceView)
        );
    }
    
    public void Bind(ref PeanutGraphics graphics)
    {
        graphics.GetContext.PSSetShaderResources(0u, 1u, ref resourceView);
    }
}