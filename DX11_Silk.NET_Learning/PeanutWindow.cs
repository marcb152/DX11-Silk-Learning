/////////////////////////////////////////////////////// PLEASE READ! ///////////////////////////////////////////////////
// This provides a basic example of using our Direct3D 11 bindings in their current form. These bindings are still    //
// improving over time, and as a result the content of this example may change.                                       //
// Notably:                                                                                                           //
// TODO remove Unsafe.NullRef once we've updated the bindings to not require it                                       //
// TODO investigate making the D3DPrimitiveTopology enum more user friendly                                           //
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Numerics;
using DX11_Silk.NET_Learning.ImGui_DX11_Impl;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace DX11_Silk.NET_Learning;

public class PeanutWindow
{
    private Vector2 mousePos = Vector2.Zero;
    
    private Vector2 cameraDir = Vector2.Zero;
    private Vector3 cameraPos = new Vector3(0, 0, -10);
    private float cameraSpeed = 10f;
    
    private Dictionary<Key, bool> pressedKeys = new Dictionary<Key, bool>();
    
    private IWindow? window;
    
    private PeanutGraphics? graphics;

    private ImGuiDX11Controller controller;

    public PeanutWindow()
    {
        // Init pressed keys dictionary.
        foreach (Key key in Enum.GetValues(typeof(Key)).Cast<Key>())
        {
            pressedKeys[key] = false;
        }
        // Create a window.
        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(800, 600),
            Title = "Learn Direct3D11 with Silk.NET",
            API = GraphicsAPI.None, // <-- This bit is important, as your window will be configured for OpenGL by default.
        };
        window = Window.Create(options);
        
        controller = null;
        
        // Assign events.
        window.Load += OnLoad;
        window.Update += OnUpdate;
        window.Render += OnRender;
        window.FramebufferResize += OnFramebufferResize;
        
        // Run the window.
        window.Run();

        // Dispose of the ImGui context.
        controller?.Dispose();
        
        // Dispose of the graphics object.
        graphics?.Dispose();

        //dispose the window, and its internal resources
        window.Dispose();
    }

    private unsafe void OnLoad()
    {
        // Set-up input context.
        IInputContext input = window!.CreateInput();
        foreach (var keyboard in input.Keyboards)
        {
            keyboard.KeyDown += OnKeyDown;
            keyboard.KeyUp += OnKeyUp;
        }
        input.Mice[0].MouseMove += OnMouseMove;
        
        // Init ImGui.
        // IntPtr ctx = ImGui.CreateContext();
        // ImGui.SetCurrentContext(ctx);
        // ImGui.StyleColorsDark();

        // ImGui.ImGui_ImplWin32_Init(window!.Native!.DXHandle!.Value);
        // ImGuiViewportPtr mainVP = ImGui.GetMainViewport();
        // mainVP.PlatformHandle = window!.Native!.DXHandle!.Value;
        // mainVP.PlatformHandleRaw = window!.Native!.DXHandle!.Value;
        
        // TODO: Add ImGui windows messages handling (aka events).
        // ImGui.ImGui_ImplWin32_WndProcHandler()
        // ImGui.GetIO().ConfigDebugIsDebuggerPresent = true;
        
        // Create a graphics object.
        graphics = new PeanutGraphics(window!, ref controller, ref input);
    }


    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        mousePos = position;
    }

    private void OnUpdate(double deltaSeconds)
    {
        // Here all of the updates to program state ahead of rendering (e.g. physics) should be done. We don't have anything
        // to do here at the moment, so we've left it blank.
        cameraPos.X += cameraDir.X * (float)deltaSeconds * cameraSpeed;
        cameraPos.Y += cameraDir.Y * (float)deltaSeconds * cameraSpeed;
    }

    private unsafe void OnFramebufferResize(Vector2D<int> newSize)
    {
        graphics?.OnFramebufferResize(newSize);
    }

    private unsafe void OnRender(double deltaSeconds)
    {
        controller.Update((float)deltaSeconds);
        // Time is paused when the space key is pressed.
        graphics?.BeginFrame(pressedKeys[Key.Space] ? 0f : deltaSeconds);
        graphics?.Draw(false, cameraPos, window!.FramebufferSize, pressedKeys[Key.Space] ? 0f : deltaSeconds);
        
        // ImGui
        // ImGui.ImGui_ImplWin32_NewFrame();
        // ImGuiIOPtr io = ImGui.GetIO();
        // io.DisplaySize = new Vector2(window!.FramebufferSize.X, window!.FramebufferSize.Y);
        // io.DisplayFramebufferScale = new Vector2(1, 1);
        // io.DeltaTime = (float)deltaSeconds;
        // io.Fonts.AddFontDefault();
        // instance.ImGui_ImplDX11_NewFrame();
        // ImGui.NewFrame();
        
        // bool showDemoWindow = true;
        // if (showDemoWindow)
        // {
        //     ImGui.ShowDemoWindow(ref showDemoWindow);
        // }
        // ImGui.Render();
        // instance.ImGui_ImplDX11_RenderDrawData(ImGui.GetDrawData());
        //
        // ImGui.ShowDemoWindow();
        ImGui.ShowMetricsWindow();
        controller.Render();
        
        graphics?.EndFrame();
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int scancode)
    {
        // Check to close the window on escape.
        if (key == Key.Escape)
        {
            window?.Close();
        }
        pressedKeys[key] = true;
        switch (key)
        {
            case Key.W:
                cameraDir.X = -1;
                break;
            case Key.S:
                cameraDir.X = 1;
                break;
            case Key.A:
                cameraDir.Y = 1;
                break;
            case Key.D:
                cameraDir.Y = -1;
                break;
        }
    }
    
    private void OnKeyUp(IKeyboard keyboard, Key key, int scancode)
    {
        pressedKeys[key] = false;
        switch (key)
        {
            case Key.W:
                cameraDir.X = 0;
                break;
            case Key.S:
                cameraDir.X = 0;
                break;
            case Key.A:
                cameraDir.Y = 0;
                break;
            case Key.D:
                cameraDir.Y = 0;
                break;
        }
    }
}