using System.Numerics;
using System.Runtime.CompilerServices;
using DX11_Silk.NET_Learning.Drawables;
using DX11_Silk.NET_Learning.ImGui_DX11_Impl;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace DX11_Silk.NET_Learning;

public class PeanutGraphics : IDisposable
{
    private readonly ImGui_Impl_DX11 _instance;
    public Matrix4x4 ProjectionMatrix { get; set; }
    
    private float[] backgroundColor = [0.2f, 0.2f, 0.2f, 1.0f];

    private double elapsedTime = 0.0f;

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
    public ComPtr<ID3D11Device> GetDevice => device;
    
    private ComPtr<ID3D11DeviceContext> deviceContext = default;
    
    public ComPtr<ID3D11DeviceContext> GetContext => deviceContext;
    
    private ComPtr<ID3D11RenderTargetView> renderTargetView = default;
    private ComPtr<ID3D11DepthStencilView> DSV = default;

    private List<Drawable> boxes = [];

    public unsafe PeanutGraphics(IView window, Vector2D<int> FramebufferSize, ref ImGui_Impl_DX11 instance, ref ImGuiDX11Controller controller, ref IInputContext input)
    {
        // _instance = instance;
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
        ImGuiFontConfig fontConfig = new ImGuiFontConfig("C:\\Windows\\Fonts\\arial.ttf", 16, ptr => ptr.Fonts.GetGlyphRangesDefault());
        controller = new ImGuiDX11Controller(device, deviceContext, window, input, instance, fontConfig);

        // Obtain the framebuffer for the swapchain's backbuffer.
        // Gets released when the execution leaves the scope thanks to using.
        using var backbuffer = swapchain.GetBuffer<ID3D11Texture2D>(0);
        // Create a view over the render target.
        SilkMarshal.ThrowHResult(device.CreateRenderTargetView(backbuffer, null, ref renderTargetView));
        
        for (int i = 0; i < 50; i++)
        {
            if (i % 2 == 0)
                boxes.Add(new CubeBox(this, ref compiler,
                    adist: new Vector2(0f, 3.1415f / 2f),
                    ddist: new Vector2(0f, 3.1415f / 2f),
                    odist: new Vector2(0f, 3.1415f / 2f),
                    rdist: new Vector2(6f, 20f)));
            else
                boxes.Add(new RenderedBlock(this, ref compiler,
                    adist: new Vector2(0f, 3.1415f / 2f),
                    ddist: new Vector2(0f, 3.1415f / 2f),
                    odist: new Vector2(0f, 3.1415f / 2f),
                    rdist: new Vector2(6f, 20f)));
        }

        ProjectionMatrix = Matrix4x4.CreatePerspectiveLeftHanded(
            1, FramebufferSize.Y / (float)FramebufferSize.X, 0.5f, 100.0f);

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
        deviceContext.OMSetRenderTargets(1u, ref renderTargetView, DSV);
        
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
        deviceContext.RSSetViewports(1u, in viewport);
        
        // Init ImGui.
        // instance.ImGui_ImplDX11_Init(device, deviceContext);
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
        deviceContext.RSSetViewports(1u, in viewport);
        
        ProjectionMatrix = Matrix4x4.CreatePerspectiveLeftHanded(
            1, newSize.Y / (float)newSize.X, 0.5f, 100.0f);

    }
    
    public unsafe void BeginFrame(double deltaSeconds)
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

    public unsafe void Draw(bool move, Vector3 cameraPos, Vector2D<int> FramebufferSize, double dt)
    {
        for (int i = 0; i < boxes.Count; i++)
        {
            boxes[i].Update(dt);
            boxes[i].Draw(this);
        }
    }

    public unsafe void EndFrame()
    {
        // Presenting the backbuffer to the swapchain.
        HResult hr = swapchain.Present(1u, 0u);
        if (hr.IsFailure)
        {
            SilkMarshal.ThrowHResult(hr);
            // TODO: Handle device removed or reset.
        }
    }
    
    public unsafe void DrawIndexed(uint indexCount)
    {
        deviceContext.DrawIndexed(indexCount, 0u, 0);
    }
    
    public void Dispose()
    {
        // Dispose of ImGui.
        // _instance.ImGui_ImplDX11_Shutdown();
        // Clean up any resources.
        factory.Dispose();
        swapchain.Dispose();
        device.Dispose();
        deviceContext.Dispose();
        renderTargetView.Dispose();
        DSV.Dispose();
        compiler.Dispose();
        d3d11.Dispose();
        dxgi.Dispose();
    }
}