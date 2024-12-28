/////////////////////////////////////////////////////// PLEASE READ! ///////////////////////////////////////////////////
// This provides a basic example of using our Direct3D 11 bindings in their current form. These bindings are still    //
// improving over time, and as a result the content of this example may change.                                       //
// Notably:                                                                                                           //
// TODO remove Unsafe.NullRef once we've updated the bindings to not require it                                       //
// TODO investigate making the D3DPrimitiveTopology enum more user friendly                                           //
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Numerics;
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
    
    private IWindow? window;
    
    private PeanutGraphics? graphics;

    public PeanutWindow()
    {
        // Create a window.
        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(800, 600),
            Title = "Learn Direct3D11 with Silk.NET",
            API = GraphicsAPI.None, // <-- This bit is important, as your window will be configured for OpenGL by default.
        };
        window = Window.Create(options);

        // Assign events.
        window.Load += OnLoad;
        window.Update += OnUpdate;
        window.Render += OnRender;
        window.FramebufferResize += OnFramebufferResize;

        // Run the window.
        window.Run();

        // Dispose of the graphics object.
        graphics?.Dispose();

        //dispose the window, and its internal resources
        window.Dispose();
    }

    private unsafe void OnLoad()
    {
        
        // Set-up input context.
        var input = window!.CreateInput();
        foreach (var keyboard in input.Keyboards)
        {
            keyboard.KeyDown += OnKeyDown;
            keyboard.KeyUp += OnKeyUp;
        }
        input.Mice[0].MouseMove += OnMouseMove;

        // Create a graphics object.
        graphics = new PeanutGraphics(window!, window!.FramebufferSize);
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
        graphics?.BeginFrame(deltaSeconds);
        graphics?.Draw(false, cameraPos, window!.FramebufferSize, deltaSeconds);
        graphics?.EndFrame();
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int scancode)
    {
        // Check to close the window on escape.
        if (key == Key.Escape)
        {
            window?.Close();
        }
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