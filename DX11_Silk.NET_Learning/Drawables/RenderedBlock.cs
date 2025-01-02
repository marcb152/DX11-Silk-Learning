using System.Numerics;
using System.Runtime.InteropServices;
using DX11_Silk.NET_Learning.Bindables;
using DX11_Silk.NET_Learning.Models;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using StbImageSharp;

namespace DX11_Silk.NET_Learning.Drawables;

public class RenderedBlock : Drawable.DrawableBase<RenderedBlock>
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
    
    private TexturedVertex[] vertices =
    [
        // Front face (z = 1)
        new TexturedVertex { position = new Vector3(0, 0, 1), texCoord = new Vector2(1, 1) },  // Bottom Left
        new TexturedVertex { position = new Vector3(1, 0, 1), texCoord = new Vector2(0, 1) },  // Bottom Right
        new TexturedVertex { position = new Vector3(1, 1, 1), texCoord = new Vector2(0, 0) },  // Top Right
        new TexturedVertex { position = new Vector3(0, 1, 1), texCoord = new Vector2(1, 0) },  // Top Left

        // Back face (z = 0)
        new TexturedVertex { position = new Vector3(0, 0, 0), texCoord = new Vector2(1, 1) },  // Bottom Left
        new TexturedVertex { position = new Vector3(1, 0, 0), texCoord = new Vector2(0, 1) },  // Bottom Right
        new TexturedVertex { position = new Vector3(1, 1, 0), texCoord = new Vector2(0, 0) },  // Top Right
        new TexturedVertex { position = new Vector3(0, 1, 0), texCoord = new Vector2(1, 0) },  // Top Left

        // Left face (x = 0)
        new TexturedVertex { position = new Vector3(0, 0, 0), texCoord = new Vector2(1, 1) },  // Bottom Front
        new TexturedVertex { position = new Vector3(0, 0, 1), texCoord = new Vector2(0, 1) },  // Bottom Back
        new TexturedVertex { position = new Vector3(0, 1, 1), texCoord = new Vector2(0, 0) },  // Top Back
        new TexturedVertex { position = new Vector3(0, 1, 0), texCoord = new Vector2(1, 0) },  // Top Front

        // Right face (x = 1)
        new TexturedVertex { position = new Vector3(1, 0, 0), texCoord = new Vector2(1, 1) },  // Bottom Front
        new TexturedVertex { position = new Vector3(1, 0, 1), texCoord = new Vector2(0, 1) },  // Bottom Back
        new TexturedVertex { position = new Vector3(1, 1, 1), texCoord = new Vector2(0, 0) },  // Top Back
        new TexturedVertex { position = new Vector3(1, 1, 0), texCoord = new Vector2(1, 0) },  // Top Front

        // Top face (y = 1)
        new TexturedVertex { position = new Vector3(0, 1, 0), texCoord = new Vector2(0, 0) },  // Bottom Left
        new TexturedVertex { position = new Vector3(1, 1, 0), texCoord = new Vector2(1, 0) },  // Bottom Right
        new TexturedVertex { position = new Vector3(1, 1, 1), texCoord = new Vector2(1, 1) },  // Top Right
        new TexturedVertex { position = new Vector3(0, 1, 1), texCoord = new Vector2(0, 1) },  // Top Left

        // Bottom face (y = 0)
        new TexturedVertex { position = new Vector3(0, 0, 0), texCoord = new Vector2(0, 0) },  // Top Left
        new TexturedVertex { position = new Vector3(1, 0, 0), texCoord = new Vector2(1, 0) },  // Top Right
        new TexturedVertex { position = new Vector3(1, 0, 1), texCoord = new Vector2(1, 1) },  // Bottom Right
        new TexturedVertex { position = new Vector3(0, 0, 1), texCoord = new Vector2(0, 1) } // Bottom Left
    ];

    private ushort[] indices =
    [
        // Front face
        1, 2, 0, 2, 3, 0,
        // Back face
        5, 4, 6, 6, 4, 7,
        // Left face
        10, 8, 9, 11, 8, 10,
        // Right face
        14, 13, 12, 15, 14, 12,
        // Top face
        17, 16, 18, 18, 16, 19,
        // Bottom face
        22, 20, 21, 23, 20, 22,
    ];

    [StructLayout(LayoutKind.Explicit, Pack = 16, Size = 16)]
    struct PixelShaderConstants
    {
        [FieldOffset(0)]
        public uint sides;
        [FieldOffset(4)]
        public uint top;
        [FieldOffset(8)]
        public uint bottom;
    }

    private PixelShaderConstants pixelShaderConstants = new()
    {
        sides = 1,
        top = 0,
        bottom = 2
    };
    
    public unsafe RenderedBlock(PeanutGraphics graphics, ref D3DCompiler compiler, Vector2 adist, Vector2 ddist, Vector2 odist, Vector2 rdist)
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
            AddStaticBind(new VertexBuffer<TexturedVertex>(ref graphics, ref vertices));

            byte[] imageBytes = File.ReadAllBytes("Resources/Textures/blocks.png");
            ImageResult image = ImageResult.FromMemory(imageBytes, ColorComponents.RedGreenBlueAlpha);
            AddStaticBind(new PeanutTexture(ref graphics, ref image));
            
            AddStaticBind(new Sampler(ref graphics));
            
            AddStaticBind(new PixelConstantBuffer<PixelShaderConstants>(ref graphics, ref pixelShaderConstants));
            
            string path = Path.Combine(Directory.GetCurrentDirectory(),
                "Shaders/TextureVS.hlsl");
            VertexShader vertexShader = new VertexShader(ref graphics, ref compiler, ref path);
            AddStaticBind(vertexShader);

            path = Path.Combine(Directory.GetCurrentDirectory(),
                "Shaders/TexturePS.hlsl");
            AddStaticBind(new PixelShader(ref graphics, ref compiler, ref path));

            AddStaticIndexBuffer(new IndexBuffer(ref graphics, ref indices));

            // Input layout
            fixed (byte* pos = SilkMarshal.StringToMemory("Position"))
            fixed (byte* texCoord = SilkMarshal.StringToMemory("TexCoord"))
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
                        SemanticName = texCoord,
                        SemanticIndex = 0,
                        Format = Format.FormatR32G32Float,
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
        return Matrix4x4.CreateScale(2f) *
               Matrix4x4.CreateFromYawPitchRoll(yaw, pitch, roll) *
               Matrix4x4.CreateTranslation(r, 0f, 0f) *
               Matrix4x4.CreateFromYawPitchRoll(theta, phi, chi) *
               Matrix4x4.CreateTranslation(0f, 0f, 20f);
    }
    
    private static float RandomRange(Random rdm, float min, float max)
    {
        return min + (float)rdm.NextDouble() * (max - min);
    }
}