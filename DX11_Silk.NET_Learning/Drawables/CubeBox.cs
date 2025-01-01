using System.Numerics;
using DX11_Silk.NET_Learning.Bindables;
using DX11_Silk.NET_Learning.Models;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;

namespace DX11_Silk.NET_Learning.Drawables;

public class CubeBox : Drawable.DrawableBase<CubeBox>
{
    private float r;
    private float roll = 0f;
    private float pitch = 0f;
    private float yaw = 0f;
    private float theta;
    private float phi;
    private float chi;
    // Speed (delta/s)
    private float droll;
    private float dpitch;
    private float dyaw;
    private float dtheta;
    private float dphi;
    private float dchi;
    
    private Vertex[] vertices =
    [
        new Vertex() { position = new Vector3(-1.0f, -1.0f, -1.0f), color = new Vertex.Color() { r = 255, g = 0, b = 0, a = 255 }},
        new Vertex() { position = new Vector3(1.0f, -1.0f, -1.0f), color = new Vertex.Color() { r = 0, g = 255, b = 0, a = 255 }},
        new Vertex() { position = new Vector3(-1.0f, 1.0f, -1.0f), color = new Vertex.Color() { r = 0, g = 0, b = 255, a = 255 }},
        new Vertex() { position = new Vector3(1.0f, 1.0f, -1.0f), color = new Vertex.Color() { r = 255, g = 255, b = 0, a = 255 }},
        new Vertex() { position = new Vector3(-1.0f, -1.0f, 1.0f), color = new Vertex.Color() { r = 255, g = 0, b = 255, a = 255 }},
        new Vertex() { position = new Vector3(1.0f, -1.0f, 1.0f), color = new Vertex.Color() { r = 0, g = 255, b = 255, a = 255 }},
        new Vertex() { position = new Vector3(-1.0f, 1.0f, 1.0f), color = new Vertex.Color() { r = 0, g = 0, b = 0, a = 255 }},
        new Vertex() { position = new Vector3(1.0f, 1.0f, 1.0f), color = new Vertex.Color() { r = 255, g = 255, b = 255, a = 255 }},
    ];

    // private Vector3[] vertices2 =
    // [
    //     new Vector3(-1f, -1f, -1f),
    //     new Vector3(1f, -1f, -1f),
    //     new Vector3(-1f, 1f, -1f),
    //     new Vector3(1f, 1f, -1f),
    //     new Vector3(-1f, -1f, 1f),
    //     new Vector3(1f, -1f, 1f),
    //     new Vector3(-1f, 1f, 1f),
    //     new Vector3(1f, 1f, 1f)
    // ];

    private ushort[] indices =
    [
        0,2,1, 2,3,1,
        1,3,5, 3,7,5,
        2,6,3, 3,6,7,
        4,5,7, 4,7,6,
        0,4,2, 2,4,6,
        0,1,4, 1,5,4
    ];
    
    public unsafe CubeBox(PeanutGraphics graphics, ref D3DCompiler compiler, Vector2 adist, Vector2 ddist, Vector2 odist, Vector2 rdist)
    {
        Random random = new Random();
        r = RandomRange(random, rdist.X, rdist.Y);
        droll = RandomRange(random, ddist.X, ddist.Y);
        dpitch = RandomRange(random, ddist.X, ddist.Y);
        dyaw = RandomRange(random, ddist.X, ddist.Y);
        dphi = RandomRange(random, odist.X, odist.Y);
        dtheta = RandomRange(random, odist.X, odist.Y);
        dchi = RandomRange(random, odist.X, odist.Y);
        theta = RandomRange(random, adist.X, adist.Y);
        phi = RandomRange(random, adist.X, adist.Y);
        chi = RandomRange(random, adist.X, adist.Y);

        if (!IsStaticInitialized)
        {
            AddStaticBind(new VertexBuffer<Vertex>(ref graphics, ref vertices));

            string path = Path.Combine(Directory.GetCurrentDirectory(),
                "Shaders/VertexShader.hlsl");
            VertexShader vertexShader = new VertexShader(ref graphics, ref compiler, ref path);
            AddStaticBind(vertexShader);

            path = Path.Combine(Directory.GetCurrentDirectory(),
                "Shaders/PixelShader.hlsl");
            AddStaticBind(new PixelShader(ref graphics, ref compiler, ref path));

            AddStaticIndexBuffer(new IndexBuffer(ref graphics, ref indices));

            // Input layout
            fixed (byte* pos = SilkMarshal.StringToMemory("Position"),
                   color = SilkMarshal.StringToMemory("Color"))
            {
                ReadOnlySpan<InputElementDesc> vertexStructureDesc =
                [
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
                AddStaticBind(new InputLayout(ref graphics, ref vertexStructureDesc, ref vertexShader.GetBytecodeBlob));
            }

            AddStaticBind(new Topology(D3DPrimitiveTopology.D3D11PrimitiveTopologyTrianglelist));
        }
        else
        {
            SetIndexFromStatic();
        }

        AddBind(new TransformConstBuff(ref graphics, this));
    }
    
    public override void Update(double deltaTime)
    {
        float dt = (float)deltaTime;
        roll += droll * dt;
        pitch += dpitch * dt;
        yaw += dyaw * dt;
        theta += dtheta * dt;
        phi += dphi * dt;
        chi += dchi * dt;
    }

    public override Matrix4x4 GetTransform()
    {
        return Matrix4x4.CreateFromYawPitchRoll(yaw, pitch, roll) *
               Matrix4x4.CreateTranslation(r, 0f, 0f) *
               Matrix4x4.CreateFromYawPitchRoll(theta, phi, chi) *
               Matrix4x4.CreateTranslation(0f, 0f, 20f);
    }
    
    private static float RandomRange(Random rdm, float min, float max)
    {
        return min + (float)rdm.NextDouble() * (max - min);
    }
}