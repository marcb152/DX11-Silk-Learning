using System.Runtime.CompilerServices;
using System.Text;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using Silk.NET.Direct3D11;

namespace DX11_Silk.NET_Learning.Bindables;

public class VertexShader : IBindable
{
    private ComPtr<ID3D11VertexShader> vertexShader = default;
    protected ComPtr<ID3D10Blob> bytecodeBlob = default;
    
    public ref ComPtr<ID3D10Blob> GetBytecodeBlob => ref bytecodeBlob;
    
    public unsafe VertexShader(ref PeanutGraphics graphics, ref D3DCompiler compiler, ref string path)
    {
        // Compiling vertex shader blob
        var shaderBytes = Encoding.ASCII.GetBytes(File.ReadAllText(path));

        // Compile vertex shader.
        ComPtr<ID3D10Blob> vertexErrors = default;
        HResult hr = compiler.Compile
        (
            in shaderBytes[0],
            (nuint) shaderBytes.Length,
            null as string,
            null,
            ref Unsafe.NullRef<ID3DInclude>(),
            "main",
            "vs_5_0",
            0,
            0,
            ref bytecodeBlob,
            ref vertexErrors
        );
        // Check for compilation errors.
        if (hr.IsFailure)
        {
            if (vertexErrors.Handle is not null)
            {
                Console.WriteLine(SilkMarshal.PtrToString((nint) vertexErrors.GetBufferPointer()));
            }
            hr.Throw();
        }
        // Register shader
        SilkMarshal.ThrowHResult(graphics.GetDevice.CreateVertexShader(
            bytecodeBlob.GetBufferPointer(), bytecodeBlob.GetBufferSize(),
            ref Unsafe.NullRef<ID3D11ClassLinkage>(), ref vertexShader));
    }
    
    public void Bind(ref PeanutGraphics graphics)
    {
        unsafe
        {
            graphics.GetContext.VSSetShader(vertexShader, null, 0u);
        }
    }
}