using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using DX11_Silk.NET_Learning.Models;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.Maths;

namespace DX11_Silk.NET_Learning;

public class PeanutGraphics : IDisposable
{
    private float[] backgroundColor = [0.0f, 0.0f, 0.0f, 1.0f];

    private double elapsedTime = 0.0f;

    private Vertex[] vertices =
    [
        new Vertex() { x =  -1.0f, y =  -1.0f, z = -1.0f, color = new Vertex.Color() { r = 255, g = 0, b = 0, a = 255 }},
        new Vertex() { x =  1.0f, y = -1.0f, z = -1.0f, color = new Vertex.Color() { r = 0, g = 255, b = 0, a = 255 }},
        new Vertex() { x = -1.0f, y = 1.0f, z = -1.0f, color = new Vertex.Color() { r = 0, g = 0, b = 255, a = 255 }},
        new Vertex() { x = 1.0f, y = 1.0f, z = -1.0f, color = new Vertex.Color() { r = 255, g = 255, b = 0, a = 255 }},
        new Vertex() { x = -1.0f, y = -1.0f, z = 1.0f, color = new Vertex.Color() { r = 255, g = 0, b = 255, a = 255 }},
        new Vertex() { x = 1.0f, y = -1.0f, z = 1.0f, color = new Vertex.Color() { r = 0, g = 255, b = 255, a = 255 }},
        new Vertex() { x = -1.0f, y = 1.0f, z = 1.0f, color = new Vertex.Color() { r = 0, g = 0, b = 0, a = 255 }},
        new Vertex() { x = 1.0f, y = 1.0f, z = 1.0f, color = new Vertex.Color() { r = 255, g = 255, b = 255, a = 255 }},
    ];

    private ushort[] indices =
    [
        0,2,1, 2,3,1,
        1,3,5, 3,7,5,
        2,6,3, 3,6,7,
        4,5,7, 4,7,6,
        0,4,2, 2,4,6,
        0,1,4, 1,5,4
    ];

    private struct ConstBuffStruct
    {
        public Matrix4x4 transform;
    }
    
    private ConstBuffStruct constantBufferStruct;

    
    // Load the DXGI and Direct3D11 libraries for later use.
    // Given this is not tied to the window, this doesn't need to be done in the OnLoad event.
    private DXGI dxgi = null!;

    private D3D11 d3d11 = null!;
    private D3DCompiler compiler = null!;

    // These variables are initialized within the Load event.
    private ComPtr<IDXGIFactory2> factory = default;

    private ComPtr<IDXGISwapChain1> swapchain = default;
    private ComPtr<ID3D11Device> device = default;
    private ComPtr<ID3D11DeviceContext> deviceContext = default;
    private ComPtr<ID3D11RenderTargetView> renderTargetView = default;
    private ComPtr<ID3D11Buffer> vertexBuffer = default;
    private ComPtr<ID3D11Buffer> indexBuffer = default;
    private ComPtr<ID3D11Buffer> constantBuffer = default;
    private ComPtr<ID3D11VertexShader> vertexShader = default;
    private ComPtr<ID3D11PixelShader> pixelShader = default;
    private ComPtr<ID3D11InputLayout> inputLayout = default;
    private ComPtr<ID3D11DepthStencilView> DSV = default;

    public unsafe PeanutGraphics(INativeWindowSource window, Vector2D<int> FramebufferSize)
    {
        // Whether or not to force use of DXVK on platforms where native DirectX implementations are available
        const bool forceDxvk = false;

        dxgi = DXGI.GetApi(window, forceDxvk);
        d3d11 = D3D11.GetApi(window, forceDxvk);
        compiler = D3DCompiler.GetApi();

        SwapChainDesc swapChainDesc = new SwapChainDesc
        {
            BufferDesc =
            {
                Width = 0,
                Height = 0,
                Format = Format.FormatB8G8R8A8Unorm,
                RefreshRate = new Rational(0, 0),
                Scaling = ModeScaling.Unspecified,
                ScanlineOrdering = ModeScanlineOrder.Unspecified,
            },
            SampleDesc =
            {
                Count = 1,
                Quality = 0,
            },
            BufferCount = 2,
            BufferUsage = DXGI.UsageRenderTargetOutput,
            SwapEffect = SwapEffect.Discard,
            Windowed = true,
            OutputWindow = window.Native!.DXHandle!.Value,
            Flags = 0,
        };

        // Create device and front/back buffers, and swap chain and rendering context
        SilkMarshal.ThrowHResult(
            d3d11.CreateDeviceAndSwapChain(
                default(ComPtr<IDXGIAdapter>),
                D3DDriverType.Hardware,
                default,
                (uint)CreateDeviceFlag.Debug,
                null,
                0,
                D3D11.SdkVersion,
                in swapChainDesc,
                ref swapchain,
                ref device,
                null,
                ref deviceContext
            )
        );

        //This is not supported under DXVK
        //TODO: PR a stub into DXVK for this maybe?
        if (OperatingSystem.IsWindows())
        {
            // Log debug messages for this device (given that we've enabled the debug flag). Don't do this in release code!
            device.SetInfoQueueCallback(msg =>
            {
                Console.WriteLine(SilkMarshal.PtrToString((nint)msg.PDescription));
            });
        }
        // Obtain the framebuffer for the swapchain's backbuffer.
        // Gets released when the execution leaves the scope thanks to using.
        using var backbuffer = swapchain.GetBuffer<ID3D11Texture2D>(0);
        // Create a view over the render target.
        SilkMarshal.ThrowHResult(device.CreateRenderTargetView(backbuffer, null, ref renderTargetView));
        
        // Creating vertex buffer data
        BufferDesc bufferDesc = new BufferDesc()
        {
            Usage = Usage.Default,
            ByteWidth = (uint)(vertices.Length * sizeof(Vertex)),
            MiscFlags = 0u,
            CPUAccessFlags = 0u,
            StructureByteStride = (uint)sizeof(Vertex),
            BindFlags = (uint)BindFlag.VertexBuffer,
        };
        fixed (Vertex* vertexData = vertices)
        {
            SubresourceData subresourceData = new SubresourceData
            {
                PSysMem = vertexData
            };
            SilkMarshal.ThrowHResult(device.CreateBuffer(in bufferDesc, in subresourceData, ref vertexBuffer));
        }
        
        // Creating index buffer data
        bufferDesc = new BufferDesc()
        {
            Usage = Usage.Default,
            ByteWidth = (uint)(indices.Length * sizeof(ushort)),
            MiscFlags = 0u,
            CPUAccessFlags = 0u,
            StructureByteStride = (uint)sizeof(ushort),
            BindFlags = (uint)BindFlag.IndexBuffer,
        };
        fixed (ushort* indexData = indices)
        {
            SubresourceData subresourceData = new SubresourceData
            {
                PSysMem = indexData
            };
            SilkMarshal.ThrowHResult(device.CreateBuffer(in bufferDesc, in subresourceData, ref indexBuffer));
        }
        
        // Compiling vertex shader blob
        string path = Path.Combine(Directory.GetCurrentDirectory(),
            "Shaders/VertexShader.hlsl");
        var shaderBytes = Encoding.ASCII.GetBytes(File.ReadAllText(path));

        // Compile vertex shader.
        ComPtr<ID3D10Blob> vertexCode = default;
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
            ref vertexCode,
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
        SilkMarshal.ThrowHResult(device.CreateVertexShader(
            vertexCode.GetBufferPointer(), vertexCode.GetBufferSize(),
            ref Unsafe.NullRef<ID3D11ClassLinkage>(), ref vertexShader));
        
        // Describe the layout of the input data for the vertex shader.
        fixed (byte* pos = SilkMarshal.StringToMemory("Position"),
                    color = SilkMarshal.StringToMemory("Color"))
        {
            ReadOnlySpan<InputElementDesc> vertexStructureDesc = [
                new InputElementDesc()
            {
                SemanticName = pos,
                SemanticIndex = 0,
                Format = Format.FormatR32G32B32Float,
                InputSlot = 0,
                AlignedByteOffset = 0,
                InputSlotClass = InputClassification.PerVertexData,
                InstanceDataStepRate = 0
            },
            new InputElementDesc()
            {
                SemanticName = color,
                SemanticIndex = 0,
                Format = Format.FormatB8G8R8A8Unorm,
                InputSlot = 0,
                AlignedByteOffset = D3D11.AppendAlignedElement,
                InputSlotClass = InputClassification.PerVertexData,
                InstanceDataStepRate = 0
            },
            ];

            SilkMarshal.ThrowHResult(
                device.CreateInputLayout(
                    vertexStructureDesc,
                    (uint) vertexStructureDesc.Length,
                    vertexCode.GetBufferPointer(),
                    vertexCode.GetBufferSize(),
                    inputLayout.GetAddressOf()
                ));
        }

        vertexCode.Dispose();
        vertexErrors.Dispose();
        
        // Compiling pixel shader blob
        path = Path.Combine(Directory.GetCurrentDirectory(),
            "Shaders/PixelShader.hlsl");
        shaderBytes = Encoding.ASCII.GetBytes(File.ReadAllText(path));

        // Compile pixel shader.
        ComPtr<ID3D10Blob> pixelCode = default;
        ComPtr<ID3D10Blob> pixelErrors = default;
        hr = compiler.Compile
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
            ref pixelCode,
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
        SilkMarshal.ThrowHResult(device.CreatePixelShader(
            pixelCode.GetBufferPointer(), pixelCode.GetBufferSize(),
            ref Unsafe.NullRef<ID3D11ClassLinkage>(), ref pixelShader));
        pixelCode.Dispose();
        pixelErrors.Dispose();
        
        // Create depth stencil state
        DepthStencilDesc depthStencilDesc = new DepthStencilDesc()
        {
            DepthEnable = true,
            DepthWriteMask = DepthWriteMask.All,
            DepthFunc = ComparisonFunc.Less,
            StencilEnable = false
        };
        ComPtr<ID3D11DepthStencilState> depthStencilState = default;
        SilkMarshal.ThrowHResult(device.CreateDepthStencilState(in depthStencilDesc, ref depthStencilState));
        // Bind depth stencil state to the output merger stage
        deviceContext.OMSetDepthStencilState(depthStencilState, 1u);
        
        // Create depth stencil texture buffer
        ComPtr<ID3D11Texture2D> depthStencilBuffer = default;
        Texture2DDesc depthStencilBufferDesc = new Texture2DDesc()
        {
            Width = (uint) FramebufferSize.X,
            Height = (uint) FramebufferSize.Y,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.FormatD32Float,
            SampleDesc = new SampleDesc()
            {
                Count = 1,
                Quality = 0
            },
            Usage = Usage.Default,
            BindFlags = (uint)BindFlag.DepthStencil
        };
        SilkMarshal.ThrowHResult(device.CreateTexture2D(in depthStencilBufferDesc, null, ref depthStencilBuffer));
        // Create the depth stencil view texture
        DepthStencilViewDesc depthStencilViewDesc = new DepthStencilViewDesc()
        {
            Format = Format.FormatD32Float,
            ViewDimension = DsvDimension.Texture2D,
            Texture2D = new Tex2DDsv()
            {
                MipSlice = 0
            }
        };
        SilkMarshal.ThrowHResult(device.CreateDepthStencilView(depthStencilBuffer, in depthStencilViewDesc, ref DSV));
        // Bind depth stencil view to the output merger stage
        deviceContext.OMSetRenderTargets(1u, renderTargetView.GetAddressOf(), DSV);
    }

    public unsafe void OnFramebufferResize(Vector2D<int> newSize)
    {
        // If the window resizes, we need to be sure to update the swapchain's back buffers.
        // https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/d3d10-graphics-programming-guide-dxgi#handling-window-resizing
        deviceContext.OMSetRenderTargets(0, null, ref Unsafe.NullRef<ID3D11DepthStencilView>());
        renderTargetView.Dispose();
        deviceContext.Flush();
        
        SilkMarshal.ThrowHResult
        (
            swapchain.ResizeBuffers(0u, (uint) newSize.X, (uint) newSize.Y, Format.FormatUnknown, 0)
        );
        
        using var backbuffer = swapchain.GetBuffer<ID3D11Texture2D>(0);
        SilkMarshal.ThrowHResult(device.CreateRenderTargetView(backbuffer, null, ref renderTargetView));
        
        deviceContext.OMSetRenderTargets(1u, ref renderTargetView, ref Unsafe.NullRef<ID3D11DepthStencilView>());
        Viewport viewport = new Viewport
        {
            TopLeftX = 0,
            TopLeftY = 0,
            Width = newSize.X,
            Height = newSize.Y,
            MinDepth = 0.0f,
            MaxDepth = 1.0f
        };
        deviceContext.RSSetViewports(1u, viewport);
    }

    public unsafe void OnRender(double deltaSeconds, Vector3 cameraPos, Vector2D<int> FramebufferSize)
    {
        BeginFrame(deltaSeconds);
        Draw(false, cameraPos, FramebufferSize);
        Draw(true, cameraPos, FramebufferSize);
        EndFrame();
    }
    
    
    private unsafe void BeginFrame(double deltaSeconds)
    {
        elapsedTime += deltaSeconds;
        // float c = MathF.Sin((float)elapsedTime) / 2.0f + 0.5f;
        // backgroundColor[0] = c;
        // backgroundColor[1] = c;

        // Clear the render target to be all black ahead of rendering.
        deviceContext.ClearRenderTargetView(renderTargetView, backgroundColor);
        
        // Clear the depth buffer to 1.0f and the stencil buffer to 0.
        deviceContext.ClearDepthStencilView(DSV, (uint)ClearFlag.Depth, 1.0f, 0);
    }

    private unsafe void Draw(bool move, Vector3 cameraPos, Vector2D<int> FramebufferSize)
    {
        // Registering vertex buffer
        // Update the input assembler to use our shader input layout, and associated vertex & index buffers.
        deviceContext.IASetPrimitiveTopology(D3DPrimitiveTopology.D3D10PrimitiveTopologyTrianglelist);
        deviceContext.IASetInputLayout(inputLayout);
        deviceContext.IASetVertexBuffers(
            0u, 1u,
            // Stride is the byte-size of a single vertex (3 floats)
            ref vertexBuffer, (uint)sizeof(Vertex), 0u);
        deviceContext.IASetIndexBuffer(indexBuffer, Format.FormatR16Uint, 0u);
        //
        // float x = mousePos.X / window.FramebufferSize.X * 2.0f - 1.0f;
        // float y = -mousePos.Y / window.FramebufferSize.Y * 2.0f + 1.0f;
        //
        // Set up the constant buffer
        constantBufferStruct = new ConstBuffStruct() { transform = 
            Matrix4x4.Transpose(
                Matrix4x4.CreateScale(0.5f) *
                Matrix4x4.CreateRotationZ((float)elapsedTime) *
                Matrix4x4.CreateRotationX((float)elapsedTime) *
                Matrix4x4.CreateTranslation(cameraPos.Y, 0f, move ? cameraPos.X + 10f : 10f) *
                Matrix4x4.CreatePerspectiveLeftHanded(1, 3/4f, 0.5f, 100.0f)
            )
        };
        // Update the constant buffer
        BufferDesc bufferDesc = new BufferDesc()
        {
            Usage = Usage.Dynamic,
            ByteWidth = (uint) sizeof(ConstBuffStruct),
            MiscFlags = 0u,
            CPUAccessFlags = (uint)CpuAccessFlag.Write,
            StructureByteStride = 0u,
            BindFlags = (uint)BindFlag.ConstantBuffer,
        };
        fixed (void* constBuffer = &constantBufferStruct)
        {
            SubresourceData subresourceData = new SubresourceData
            {
                PSysMem = constBuffer
            };
            SilkMarshal.ThrowHResult(device.CreateBuffer(in bufferDesc, in subresourceData, ref constantBuffer));
        }
        deviceContext.VSSetConstantBuffers(0u, 1u, ref constantBuffer);
        
        // Vertex Shader Stage
        deviceContext.VSSetShader(vertexShader, null, 0u);
        // Pixel Shader Stage
        deviceContext.PSSetShader(pixelShader, null, 0u);
        
        // Rasterizer stage
        Viewport viewport = new Viewport
        {
            TopLeftX = 0,
            TopLeftY = 0,
            Width = FramebufferSize.X,
            Height = FramebufferSize.Y,
            MinDepth = 0.0f,
            MaxDepth = 1.0f
        };
        deviceContext.RSSetViewports(1u, viewport);
        // Output Merger stage
        deviceContext.OMSetRenderTargets(1u, ref renderTargetView, DSV);
        
        deviceContext.DrawIndexed((uint)indices.Length, 0u, 0);
    }

    private unsafe void EndFrame()
    {
        // Presenting the backbuffer to the swapchain.
        HResult hr = swapchain.Present(1u, 0u);
        if (hr.IsFailure)
        {
            SilkMarshal.ThrowHResult(hr);
            // TODO: Handle device removed or reset.
        }
    }
    
    public void Dispose()
    {
        // Clean up any resources.
        factory.Dispose();
        swapchain.Dispose();
        device.Dispose();
        deviceContext.Dispose();
        renderTargetView.Dispose();
        vertexBuffer.Dispose();
        indexBuffer.Dispose();
        vertexShader.Dispose();
        constantBuffer.Dispose();
        pixelShader.Dispose();
        inputLayout.Dispose();
        DSV.Dispose();
        compiler.Dispose();
        d3d11.Dispose();
        dxgi.Dispose();
    }
}