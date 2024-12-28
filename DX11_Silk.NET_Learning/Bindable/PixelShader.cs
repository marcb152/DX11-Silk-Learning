using System.Runtime.CompilerServices;
using System.Text;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using Silk.NET.Direct3D11;

namespace DX11_Silk.NET_Learning.Bindable;

public class PixelShader : IBindable
{
    private readonly ComPtr<ID3D11PixelShader> pixelShader = default;
    
    public unsafe PixelShader(ref PeanutGraphics graphics, ref D3DCompiler compiler, ref string path)
    {
        // Compiling pixel shader blob
        var shaderBytes = Encoding.ASCII.GetBytes(File.ReadAllText(path));

        // Compile pixel shader.
        ComPtr<ID3D10Blob> bytecodeBlob = default;
        ComPtr<ID3D10Blob> pixelErrors = default;
        HResult hr = compiler.Compile
        (
            in shaderBytes[0],
            (nuint) shaderBytes.Length,
            null as string,
            null,
            ref Unsafe.NullRef<ID3DInclude>(),
            "main",
            "ps_5_0",
            0,
            0,
            ref bytecodeBlob,
            ref pixelErrors
        );
        // Check for compilation errors.
        if (hr.IsFailure)
        {
            if (pixelErrors.Handle is not null)
            {
                Console.WriteLine(SilkMarshal.PtrToString((nint) pixelErrors.GetBufferPointer()));
            }
            hr.Throw();
        }
        // Register shader
        SilkMarshal.ThrowHResult(graphics.GetDevice.CreatePixelShader(
            bytecodeBlob.GetBufferPointer(), bytecodeBlob.GetBufferSize(),
            ref Unsafe.NullRef<ID3D11ClassLinkage>(), ref pixelShader));
        
        bytecodeBlob.Dispose();
        pixelErrors.Dispose();
    }
    
    public void Bind(ref PeanutGraphics graphics)
    {
        unsafe
        {
            graphics.GetContext.PSSetShader(pixelShader, null, 0u);
        }
    }
}