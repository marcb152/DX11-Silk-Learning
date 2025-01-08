using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using ImGuiNET;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.Maths;

namespace DX11_Silk.NET_Learning.ImGui_DX11_Impl;

public class ImGui_Impl_DX11
{
    public static uint D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE = 16u;

    // DirectX11 data
    private ComPtr<ID3D11Device>               pd3dDevice =          default;
    private ComPtr<ID3D11DeviceContext>        pd3dDeviceContext =   default;
    private ComPtr<IDXGIFactory>               pFactory =            default;
    private ComPtr<ID3D11Buffer>               pVB =                 default;
    private ComPtr<ID3D11Buffer>               pIB =                 default;
    private ComPtr<ID3D11VertexShader>         pVertexShader =       default;
    private ComPtr<ID3D11InputLayout>          pInputLayout =        default;
    private ComPtr<ID3D11Buffer>               pVertexConstantBuffer=default;
    private ComPtr<ID3D11PixelShader>          pPixelShader =        default;    
    private ComPtr<ID3D11SamplerState>         pFontSampler =        default;
    private ComPtr<ID3D11ShaderResourceView>   pFontTextureView =    default;
    private ComPtr<ID3D11RasterizerState>      pRasterizerState =    default;
    private ComPtr<ID3D11BlendState>           pBlendState =         default;
    private ComPtr<ID3D11DepthStencilState>    pDepthStencilState =  default;
    private int                         VertexBufferSize = 5_000;
    private int                         IndexBufferSize = 10_000;
    
    // Do not use BackendRendererUserData, it messed up my pointers randomly
    
    #region Functions

    private unsafe void ImGui_ImplDX11_SetupRenderState(ImDrawData* draw_data, ID3D11DeviceContext* device_ctx)
    {
        // Setup viewport
        Viewport vp = new Viewport
        {
            Width = draw_data->DisplaySize.X,
            Height = draw_data->DisplaySize.Y,
            MinDepth = 0.0f,
            MaxDepth = 1.0f
        };
        vp.TopLeftX = vp.TopLeftY = 0;
        device_ctx->RSSetViewports(1, in vp);

        // Setup shader and vertex buffers
        uint stride = (uint)sizeof(ImDrawVert);
        uint offset = 0;
        
        device_ctx->IASetInputLayout(pInputLayout);
        device_ctx->IASetVertexBuffers(0, 1, ref pVB, in stride, in offset);
        // TODO: Warning there, ImDrawIdx is not defined by ImGui.NET, and uses R16_UINT by default (16-bit indices)
        // Using R32_UINT would require recompiling cimgui with the correct define
        // See https://github.com/ImGuiNET/ImGui.NET/issues/248
        // Original code: https://github.com/ocornut/imgui/blob/master/imgui.h#L260
        device_ctx->IASetIndexBuffer(pIB, Format.FormatR16Uint, 0);
        device_ctx->IASetPrimitiveTopology(D3DPrimitiveTopology.D3D11PrimitiveTopologyTrianglelist);
        device_ctx->VSSetShader(pVertexShader, null, 0);
        device_ctx->VSSetConstantBuffers(0, 1, ref pVertexConstantBuffer);
        device_ctx->PSSetShader(pPixelShader, null, 0);
        device_ctx->PSSetSamplers(0, 1, ref pFontSampler);
        device_ctx->GSSetShader(null, null, 0);
        device_ctx->HSSetShader(null, null, 0); // In theory we should backup and restore this as well.. very infrequently used..
        device_ctx->DSSetShader(null, null, 0); // In theory we should backup and restore this as well.. very infrequently used..
        device_ctx->CSSetShader(null, null, 0); // In theory we should backup and restore this as well.. very infrequently used..

        // Setup blend state
        float[] blend_factor = [0f, 0f, 0f, 0f];
        fixed (float* blend_factor_ptr = blend_factor)
        {
            device_ctx->OMSetBlendState(pBlendState, blend_factor_ptr, 0xffffffff);
        }
        device_ctx->OMSetDepthStencilState(pDepthStencilState, 0);
        device_ctx->RSSetState(pRasterizerState);
    }
    
    // Render function
    public unsafe void ImGui_ImplDX11_RenderDrawData(ImDrawDataPtr draw_data)
    {
        // Avoid rendering when minimized
        if (draw_data.DisplaySize.X <= 0.0f || draw_data.DisplaySize.Y <= 0.0f)
            return;

        ID3D11DeviceContext* deviceCtx = pd3dDeviceContext;

        // Create and grow vertex/index buffers if needed
        if ((long)pVB.Handle == IntPtr.Zero || VertexBufferSize < draw_data.TotalVtxCount)
        {
            if ((long)pVB.Handle != IntPtr.Zero)
            {
                pVB.Release();
                pVB = null;
            }
            VertexBufferSize = draw_data.TotalVtxCount + 5000;
            BufferDesc desc = new BufferDesc
            {
                Usage = Usage.Dynamic,
                ByteWidth = (uint)(VertexBufferSize * sizeof(ImDrawVert)),
                BindFlags = (uint)BindFlag.VertexBuffer,
                CPUAccessFlags = (uint)CpuAccessFlag.Write,
                MiscFlags = 0
            };
            SilkMarshal.ThrowHResult(
                pd3dDevice.CreateBuffer(in desc, null, ref pVB));
        }
        if ((long)pIB.Handle == IntPtr.Zero || IndexBufferSize < draw_data.TotalIdxCount)
        {
            if ((long)pIB.Handle != IntPtr.Zero)
            {
                pIB.Release();
                pIB = null;
            }
            IndexBufferSize = draw_data.TotalIdxCount + 10000;
            BufferDesc desc = new BufferDesc
            {
                Usage = Usage.Dynamic,
                ByteWidth = (uint)(IndexBufferSize * sizeof(ushort)),
                BindFlags = (uint)BindFlag.IndexBuffer,
                CPUAccessFlags = (uint)CpuAccessFlag.Write
            };
            SilkMarshal.ThrowHResult(
                pd3dDevice.CreateBuffer(in desc, null, ref pIB));
        }

        // Upload vertex/index data into a single contiguous GPU buffer
        MappedSubresource vtx_resource = default;
        MappedSubresource idx_resource = default;
        SilkMarshal.ThrowHResult(
            deviceCtx->Map(pVB, 0, Map.WriteDiscard, 0, ref vtx_resource));
        SilkMarshal.ThrowHResult(
            deviceCtx->Map(pIB, 0, Map.WriteDiscard, 0, ref idx_resource));
        // TODO: Check those casts, should be fine but idk
        ImDrawVert* vtx_dst = (ImDrawVert*)vtx_resource.PData;
        ushort* idx_dst = (ushort*)idx_resource.PData;
        for (int n = 0; n < draw_data.CmdListsCount; n++)
        {
            ImDrawListPtr draw_list = draw_data.CmdLists[n];
            Unsafe.CopyBlock(vtx_dst, draw_list.VtxBuffer.Data.ToPointer(), (uint)(draw_list.VtxBuffer.Size * sizeof(ImDrawVert)));
            Unsafe.CopyBlock(idx_dst, draw_list.IdxBuffer.Data.ToPointer(), (uint)(draw_list.IdxBuffer.Size * sizeof(ushort)));
            vtx_dst += draw_list.VtxBuffer.Size;
            idx_dst += draw_list.IdxBuffer.Size;
        }
        deviceCtx->Unmap(pVB, 0);
        deviceCtx->Unmap(pIB, 0);

        // Setup orthographic projection matrix into our constant buffer
        // Our visible imgui space lies from draw_data->DisplayPos (top left) to draw_data->DisplayPos+data_data->DisplaySize (bottom right). DisplayPos is (0,0) for single viewport apps.
        MappedSubresource mapped_resource = default;
        SilkMarshal.ThrowHResult(
            deviceCtx->Map(pVertexConstantBuffer, 0, Map.WriteDiscard, 0, ref mapped_resource));
        VERTEX_CONSTANT_BUFFER_DX11* constant_buffer = (VERTEX_CONSTANT_BUFFER_DX11*)mapped_resource.PData;
        float L = draw_data.DisplayPos.X;
        float R = draw_data.DisplayPos.X + draw_data.DisplaySize.X;
        float T = draw_data.DisplayPos.Y;
        float B = draw_data.DisplayPos.Y + draw_data.DisplaySize.Y;
        Matrix4X4<float> mvp = new Matrix4X4<float>(
            2.0f / (R - L), 0.0f, 0.0f, 0.0f,
            0.0f, 2.0f / (T - B), 0.0f, 0.0f,
            0.0f, 0.0f, 0.5f, 0.0f,
            (R + L) / (L - R), (T + B) / (B - T), 0.5f, 1.0f
        );
        Unsafe.CopyBlock(Unsafe.AsPointer(ref constant_buffer->mvp), Unsafe.AsPointer(ref mvp), (uint)sizeof(Matrix4X4<float>));
        deviceCtx->Unmap(pVertexConstantBuffer, 0);

        BACKUP_DX11_STATE old = new BACKUP_DX11_STATE();
        old.ScissorRectsCount = old.ViewportsCount = D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE;
        deviceCtx->RSGetScissorRects(ref old.ScissorRectsCount, old.ScissorRects);
        deviceCtx->RSGetViewports(ref old.ViewportsCount, old.Viewports);
        deviceCtx->RSGetState(ref old.RS);
        deviceCtx->OMGetBlendState(ref old.BlendState, old.BlendFactor, ref old.SampleMask);
        deviceCtx->OMGetDepthStencilState(ref old.DepthStencilState, ref old.StencilRef);
        deviceCtx->PSGetShaderResources(0, 1, ref old.PSShaderResource);
        deviceCtx->PSGetSamplers(0, 1, ref old.PSSampler);
        old.PSInstancesCount = old.VSInstancesCount = old.GSInstancesCount = 256;
        deviceCtx->PSGetShader(ref old.PS, ref old.PSInstances, ref old.PSInstancesCount);
        deviceCtx->VSGetShader(ref old.VS, ref old.VSInstances, ref old.VSInstancesCount);
        deviceCtx->VSGetConstantBuffers(0, 1, ref old.VSConstantBuffer);
        deviceCtx->GSGetShader(ref old.GS, ref old.GSInstances, ref old.GSInstancesCount);
        
        deviceCtx->IAGetPrimitiveTopology(ref old.PrimitiveTopology);
        deviceCtx->IAGetIndexBuffer(ref old.IndexBuffer, ref old.IndexBufferFormat, ref old.IndexBufferOffset);
        deviceCtx->IAGetVertexBuffers(0, 1, ref old.VertexBuffer, ref old.VertexBufferStride, ref old.VertexBufferOffset);
        deviceCtx->IAGetInputLayout(ref old.InputLayout);

        // Setup desired DX state
        ImGui_ImplDX11_SetupRenderState(draw_data, deviceCtx);

        // Render command lists
        // (Because we merged all buffers into a single one, we maintain our own offset into them)
        int global_idx_offset = 0;
        int global_vtx_offset = 0;
        Vector2 clip_off = draw_data.DisplayPos;
        for (int n = 0; n < draw_data.CmdListsCount; n++)
        {
            ImDrawListPtr draw_list = draw_data.CmdLists[n];
            for (int cmd_i = 0; cmd_i < draw_list.CmdBuffer.Size; cmd_i++)
            {
                ImDrawCmdPtr pcmd = draw_list.CmdBuffer[cmd_i];
                if ((long)pcmd.UserCallback != IntPtr.Zero)
                {
                    // User callback, registered via ImDrawList::AddCallback()
                    // (ImDrawCallback_ResetRenderState is a special callback value used by the user to request the renderer to reset render state.)
                    // TODO: Implement UserCallback to request the renderer to reset render state
                    // if (pcmd.UserCallback == ImDrawCallback_ResetRenderState)
                    //     ImGui_ImplDX11_SetupRenderState(draw_data, device);
                    // else
                    //     pcmd.UserCallback(draw_list, pcmd);
                    throw new NotImplementedException();
                }
                else
                {
                    // Project scissor/clipping rectangles into framebuffer space
                    Vector2D<float> clip_min = new Vector2D<float>(pcmd.ClipRect.X - clip_off.X, pcmd.ClipRect.Y - clip_off.Y);
                    Vector2D<float> clip_max = new Vector2D<float>(pcmd.ClipRect.Z - clip_off.X, pcmd.ClipRect.W - clip_off.Y);
                    if (clip_max.X <= clip_min.X || clip_max.Y <= clip_min.Y)
                        continue;

                    // Apply scissor/clipping rectangle
                    Box2D<int> r = new Box2D<int>((int)clip_min.X, (int)clip_min.Y, (int)clip_max.X, (int)clip_max.Y);
                    deviceCtx->RSSetScissorRects(1, in r);

                    // Bind texture, Draw
                    // TODO: This line made the program crash, because the font atlas texture was not generated and bound correctly
                    // ID3D11ShaderResourceView* texture_srv = (ID3D11ShaderResourceView*)pcmd.TextureId;
                    // deviceCtx->PSSetShaderResources(0, 1, &texture_srv);
                    deviceCtx->DrawIndexed(pcmd.ElemCount, (uint)(pcmd.IdxOffset + global_idx_offset), (int)(pcmd.VtxOffset + global_vtx_offset));
                }
            }
            global_idx_offset += draw_list.IdxBuffer.Size;
            global_vtx_offset += draw_list.VtxBuffer.Size;
        }

        // Restore modified DX state
        deviceCtx->RSSetScissorRects(old.ScissorRectsCount, old.ScissorRects);
        // TODO: Solve viewports issue
        // deviceCtx->RSSetViewports(old.ViewportsCount, old.Viewports);
        deviceCtx->RSSetState(old.RS);
        if (old.RS != null) old.RS->Release();
        deviceCtx->OMSetBlendState(old.BlendState, old.BlendFactor, old.SampleMask);
        if (old.BlendState != null) old.BlendState->Release();
        deviceCtx->OMSetDepthStencilState(old.DepthStencilState, old.StencilRef);
        if (old.DepthStencilState != null) old.DepthStencilState->Release();
        deviceCtx->PSSetShaderResources(0, 1, in old.PSShaderResource);
        if (old.PSShaderResource != null) old.PSShaderResource->Release();
        deviceCtx->PSSetSamplers(0, 1, in old.PSSampler);
        if (old.PSSampler != null) old.PSSampler->Release();
        deviceCtx->PSSetShader(old.PS, old.PSInstances, old.PSInstancesCount);
        if (old.PS != null) old.PS->Release();
        // TODO: Handle arrays of PSInstances
        // for (uint i = 0; i < old.PSInstancesCount; i++)
        // {
            if (old.PSInstances.Handle != null) old.PSInstances.Release();
        // }
        deviceCtx->VSSetShader(old.VS, old.VSInstances, old.VSInstancesCount);
        if (old.VS != null) old.VS->Release();
        deviceCtx->VSSetConstantBuffers(0, 1, in old.VSConstantBuffer);
        if (old.VSConstantBuffer != null) old.VSConstantBuffer->Release();
        deviceCtx->GSSetShader(old.GS, old.GSInstances, old.GSInstancesCount);
        if (old.GS != null) old.GS->Release();
        // for (uint i = 0; i < old.VSInstancesCount; i++)
        // {
            if (old.VSInstances.Handle != null) old.VSInstances.Release();
        // }
        deviceCtx->IASetPrimitiveTopology(old.PrimitiveTopology);
        deviceCtx->IASetIndexBuffer(old.IndexBuffer, old.IndexBufferFormat, old.IndexBufferOffset);
        if (old.IndexBuffer != null) old.IndexBuffer->Release();
        deviceCtx->IASetVertexBuffers(0, 1, in old.VertexBuffer, in old.VertexBufferStride, in old.VertexBufferOffset);
        if (old.VertexBuffer != null) old.VertexBuffer->Release();
        deviceCtx->IASetInputLayout(old.InputLayout);
        if (old.InputLayout != null) old.InputLayout->Release();
    }
    
    private unsafe void ImGui_ImplDX11_CreateFontsTexture()
    {
        // Build texture atlas
        ImGuiIOPtr io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out byte* pixels,out int width, out int height);

        // Upload texture to graphics system
        Texture2DDesc desc = new Texture2DDesc
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.FormatR8G8B8A8Unorm,
            SampleDesc = new SampleDesc()
            {
                Count = 1,
            },
            Usage = Usage.Default,
            BindFlags = (uint)BindFlag.ShaderResource,
            CPUAccessFlags = 0,
        };

        ComPtr<ID3D11Texture2D> pTexture = default;
        SubresourceData subResource = new SubresourceData
        {
            PSysMem = pixels,
            SysMemPitch = desc.Width * 4,
            SysMemSlicePitch = 0
        };
        SilkMarshal.ThrowHResult(
            pd3dDevice.CreateTexture2D(in desc, in subResource, ref pTexture));
        Debug.Assert(pTexture.Handle != null);

        // Create texture view
        ShaderResourceViewDesc srvDesc = new ShaderResourceViewDesc
        {
            Format = Format.FormatR8G8B8A8Unorm,
            ViewDimension = D3DSrvDimension.D3D11SrvDimensionTexture2D,
            Texture2D = new Tex2DSrv()
            {
                MipLevels = desc.MipLevels,
                MostDetailedMip = 0,
            },
        };
        SilkMarshal.ThrowHResult(
            pd3dDevice.CreateShaderResourceView(pTexture, in srvDesc, ref pFontTextureView));
        pTexture.Release();

        // Store our identifier
        io.Fonts.SetTexID((IntPtr)pFontTextureView.Handle);
        // Not sure where to put it, removed it from cmd buffers
        pd3dDeviceContext.PSSetShaderResources(0, 1, ref pFontTextureView);
    }
    
    
    private unsafe void ImGui_ImplDX11_DestroyFontsTexture()
    {
        if ((long)pFontTextureView.Handle != IntPtr.Zero)
        {
            pFontTextureView.Release();
            pFontTextureView = null;
            ImGui.GetIO().Fonts.SetTexID(0); // We copied data->pFontTextureView to io.Fonts->TexID so let's clear that as well.
        }
    }
    
    internal unsafe bool ImGui_ImplDX11_CreateDeviceObjects()
    {
        if ((long)pd3dDevice.Handle == IntPtr.Zero)
            return false;
        if ((long)pFontSampler.Handle != IntPtr.Zero)
            ImGui_ImplDX11_InvalidateDeviceObjects();

        // By using D3DCompile() from <d3dcompiler.h> / d3dcompiler.lib, we introduce a dependency to a given version of d3dcompiler_XX.dll (see D3DCOMPILER_DLL_A)
        // If you would like to use this DX11 sample code but remove this dependency you can:
        //  1) compile once, save the compiled shader blobs into a file or source code and pass them to CreateVertexShader()/CreatePixelShader() [preferred solution]
        //  2) use code to detect any version of the DLL and grab a pointer to D3DCompile from the DLL.
        // See https://github.com/ocornut/imgui/pull/638 for sources and details.

        // Create the vertex shader
        string vertexShader =
            @"cbuffer vertexBuffer : register(b0)
            {
              float4x4 ProjectionMatrix;
            };
            struct VS_INPUT
            {
              float2 pos : POSITION;
              float4 col : COLOR0;
              float2 uv  : TEXCOORD0;
            };
            
            struct PS_INPUT
            {
              float4 pos : SV_POSITION;
              float4 col : COLOR0;
              float2 uv  : TEXCOORD0;
            };
            
            PS_INPUT main(VS_INPUT input)
            {
              PS_INPUT output;
              output.pos = mul( ProjectionMatrix, float4(input.pos.xy, 0.f, 1.f));
              output.col = input.col;
              output.uv  = input.uv;
              return output;
            }";

        ComPtr<ID3D10Blob> vertexShaderBlob = default;
        ComPtr<ID3D10Blob> errorBlob = default;
        D3DCompiler compiler = D3DCompiler.GetApi();
        byte[] shaderBytes = Encoding.ASCII.GetBytes(vertexShader);
        HResult hr = compiler.Compile(
            in shaderBytes[0],
            (nuint) shaderBytes.Length,
            nameof(vertexShader),
            null,
            ref Unsafe.NullRef<ID3DInclude>(),
            "main",
            "vs_4_0",
            0,
            0,
            ref vertexShaderBlob,
            ref errorBlob);
        if (hr.IsFailure)
        {
            if (errorBlob.Handle is not null)
            {
                Console.WriteLine(SilkMarshal.PtrToString((nint) errorBlob.GetBufferPointer()));
            }

            hr.Throw();
            return false; // NB: Pass ID3DBlob* pErrorBlob to D3DCompile() to get error showing in (const char*)pErrorBlob->GetBufferPointer(). Make sure to Release() the blob!
        }

        SilkMarshal.ThrowHResult(
            pd3dDevice.CreateVertexShader(vertexShaderBlob.GetBufferPointer(), vertexShaderBlob.GetBufferSize(),
            ref Unsafe.NullRef<ID3D11ClassLinkage>(), ref pVertexShader));
        // TODO: Release with a try/catch block
        // {
        //     vertexShaderBlob.Release();
        //     return false;
        // }

        fixed (byte* pos = SilkMarshal.StringToMemory("POSITION"))
        fixed (byte* uv = SilkMarshal.StringToMemory("TEXCOORD"))
        fixed (byte* col = SilkMarshal.StringToMemory("COLOR"))
        {
            // Create the input layout
            ReadOnlySpan<InputElementDesc> local_layout =
            [
                new InputElementDesc()
                {
                    SemanticName = pos,
                    SemanticIndex = 0,
                    Format = Format.FormatR32G32Float,
                    InputSlot = 0,
                    AlignedByteOffset = (uint)Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.pos)),
                    InputSlotClass = InputClassification.PerVertexData,
                    InstanceDataStepRate = 0
                },
                new InputElementDesc()
                {
                    SemanticName = uv,
                    SemanticIndex = 0,
                    Format = Format.FormatR32G32Float,
                    InputSlot = 0,
                    AlignedByteOffset = (uint)Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.uv)),
                    InputSlotClass = InputClassification.PerVertexData,
                    InstanceDataStepRate = 0
                },
                new InputElementDesc()
                {
                    SemanticName = col,
                    SemanticIndex = 0,
                    Format = Format.FormatR8G8B8A8Unorm,
                    InputSlot = 0,
                    AlignedByteOffset = (uint)Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.col)),
                    InputSlotClass = InputClassification.PerVertexData,
                    InstanceDataStepRate = 0
                },
            ];
            SilkMarshal.ThrowHResult(
                pd3dDevice.CreateInputLayout(local_layout, 3,
                    vertexShaderBlob.GetBufferPointer(), (uint)vertexShaderBlob.GetBufferSize(),
                    pInputLayout.GetAddressOf()));
            // TODO: Release with a try/catch block
            // {
            //     vertexShaderBlob.Release();
            //     return false;
            // }
        }
        vertexShaderBlob.Release();
        // Release the error blob
        errorBlob.Release();

        // Create the constant buffer
        BufferDesc desc = new BufferDesc
        {
            ByteWidth = (uint)sizeof(VERTEX_CONSTANT_BUFFER_DX11),
            Usage = Usage.Dynamic,
            BindFlags = (uint)BindFlag.ConstantBuffer,
            CPUAccessFlags = (uint)CpuAccessFlag.Write,
            MiscFlags = 0
        };
        SilkMarshal.ThrowHResult(
            pd3dDevice.CreateBuffer(in desc, null, ref pVertexConstantBuffer));

        // Create the pixel shader
        string pixelShader =
            @"struct PS_INPUT
            {
            float4 pos : SV_POSITION;
            float4 col : COLOR0;
            float2 uv  : TEXCOORD0;
            };
            sampler sampler0;
            Texture2D texture0;
            
            float4 main(PS_INPUT input) : SV_Target
            {
            float4 out_col = input.col * texture0.Sample(sampler0, input.uv);
            return out_col;
            }";

        ComPtr<ID3D10Blob> pixelShaderBlob = default;
        errorBlob = null;
        shaderBytes = Encoding.ASCII.GetBytes(pixelShader);
        hr = compiler.Compile(
            in shaderBytes[0],
            (nuint) shaderBytes.Length,
            nameof(pixelShader),
            null,
            ref Unsafe.NullRef<ID3DInclude>(),
            "main",
            "ps_4_0",
            0,
            0,
            ref pixelShaderBlob,
            ref errorBlob);
        if (hr.IsFailure)
        {
            if (errorBlob.Handle is not null)
            {
                Console.WriteLine(SilkMarshal.PtrToString((nint) errorBlob.GetBufferPointer()));
            }

            hr.Throw();
            return false; // NB: Pass ID3DBlob* pErrorBlob to D3DCompile() to get error showing in (const char*)pErrorBlob->GetBufferPointer(). Make sure to Release() the blob!
        }

        SilkMarshal.ThrowHResult(
            pd3dDevice.CreatePixelShader(pixelShaderBlob.GetBufferPointer(), pixelShaderBlob.GetBufferSize(),
                ref Unsafe.NullRef<ID3D11ClassLinkage>(), ref pPixelShader));
        // TODO: Release with a try/catch block
        // {
        //     pixelShaderBlob.Release();
        //     return false;
        // }
        pixelShaderBlob.Release();
        // Release the error blob
        errorBlob.Release();

        // Create the blending setup
        BlendDesc blendDesc = new BlendDesc
        {
            AlphaToCoverageEnable = false,
            RenderTarget = new BlendDesc.RenderTargetBuffer()
            {
                Element0 = new RenderTargetBlendDesc()
                {
                    BlendEnable = true,
                    SrcBlend = Blend.SrcAlpha,
                    DestBlend = Blend.InvSrcAlpha,
                    BlendOp = BlendOp.Add,
                    SrcBlendAlpha = Blend.One,
                    DestBlendAlpha = Blend.InvSrcAlpha,
                    BlendOpAlpha = BlendOp.Add,
                    RenderTargetWriteMask = (byte)ColorWriteEnable.All,
                }
            }
        };
        SilkMarshal.ThrowHResult(
            pd3dDevice.CreateBlendState(in blendDesc, ref pBlendState));

        // Create the rasterizer state
        RasterizerDesc rasterizerDesc = new RasterizerDesc
        {
            FillMode = FillMode.Solid,
            CullMode = CullMode.None,
            ScissorEnable = true,
            DepthClipEnable = true
        };
        SilkMarshal.ThrowHResult(
            pd3dDevice.CreateRasterizerState(in rasterizerDesc, ref pRasterizerState));

        // Create depth-stencil State
        DepthStencilDesc depthStencilDesc = new DepthStencilDesc
        {
            DepthEnable = false,
            DepthWriteMask = DepthWriteMask.All,
            DepthFunc = ComparisonFunc.Always,
            StencilEnable = false,
            FrontFace = new DepthStencilopDesc()
            {
                StencilFailOp = StencilOp.Keep,
                StencilDepthFailOp = StencilOp.Keep,
                StencilPassOp = StencilOp.Keep,
                StencilFunc = ComparisonFunc.Always
            },
        };
        depthStencilDesc.BackFace = depthStencilDesc.FrontFace;
        SilkMarshal.ThrowHResult(
            pd3dDevice.CreateDepthStencilState(in depthStencilDesc, ref pDepthStencilState));

        // Create texture sampler
        // (Bilinear sampling is required by default. Set 'io.Fonts->Flags |= ImFontAtlasFlags_NoBakedLines' or 'style.AntiAliasedLinesUseTex = false' to allow point/nearest sampling)
        SamplerDesc samplerDesc = new SamplerDesc
        {
            Filter = Filter.MinMagMipLinear,
            AddressU = TextureAddressMode.Clamp,
            AddressV = TextureAddressMode.Clamp,
            AddressW = TextureAddressMode.Clamp,
            MipLODBias = 0f,
            ComparisonFunc = ComparisonFunc.Always,
            MinLOD = 0f,
            MaxLOD = 0f
        };
        SilkMarshal.ThrowHResult(
            pd3dDevice.CreateSamplerState(in samplerDesc, ref pFontSampler));

        ImGui_ImplDX11_CreateFontsTexture();

        return true;
    }
    
    
    private unsafe void ImGui_ImplDX11_InvalidateDeviceObjects()
    {
        if ((long)pd3dDevice.Handle == IntPtr.Zero)
            return;

        ImGui_ImplDX11_DestroyFontsTexture();

        if (pFontSampler.Handle != null)           { pFontSampler.Release(); pFontSampler = null; }
        if (pIB.Handle != null)                    { pIB.Release(); pIB = null; }
        if (pVB.Handle != null)                    { pVB.Release(); pVB = null; }
        if (pBlendState.Handle != null)            { pBlendState.Release(); pBlendState = null; }
        if (pDepthStencilState.Handle != null)     { pDepthStencilState.Release(); pDepthStencilState = null; }
        if (pRasterizerState.Handle != null)       { pRasterizerState.Release(); pRasterizerState = null; }
        if (pPixelShader.Handle != null)           { pPixelShader.Release(); pPixelShader = null; }
        if (pVertexConstantBuffer.Handle != null)  { pVertexConstantBuffer.Release(); pVertexConstantBuffer = null; }
        if (pInputLayout.Handle != null)           { pInputLayout.Release(); pInputLayout = null; }
        if (pVertexShader.Handle != null)          { pVertexShader.Release(); pVertexShader = null; }
    }

    
    public unsafe bool ImGui_ImplDX11_Init(ID3D11Device* device, ID3D11DeviceContext* device_context)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        
        // TODO: Check version macro
        // IMGUI_CHECKVERSION();
        // Debug.Assert(io.BackendRendererUserData == IntPtr.Zero, "Already initialized a renderer backend!");

        // Setup backend capabilities flags
        // Do not use BackendRendererUserData it messed my pointers randomly
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;  // We can honor the ImDrawCmd::VtxOffset field, allowing for large meshes.

        // Get factory from device
        ComPtr<IDXGIDevice> pDXGIDevice = default;
        ComPtr<IDXGIAdapter> pDXGIAdapter = default;
        ComPtr<IDXGIFactory> pFactory = default;

        if (device->QueryInterface(out pDXGIDevice) == 0)
        {
            if (pDXGIDevice.GetParent(out pDXGIAdapter) == 0)
            {
                if (pDXGIAdapter.GetParent(out pFactory) == 0)
                {
                    pd3dDevice = device;
                    pd3dDeviceContext = device_context;
                    this.pFactory = pFactory;
                }
            }
        }
        pDXGIDevice.Release();
        pDXGIAdapter.Release();
        pd3dDevice.AddRef();
        pd3dDeviceContext.AddRef();

        return true;
    }
    
    public unsafe void ImGui_ImplDX11_Shutdown()
    {
        //TODO
        // Debug.Assert(bd is not default(ImGui_ImplDX11_Data), "No renderer backend to shutdown, or already shutdown?");
        ImGuiIOPtr io = ImGui.GetIO();

        ImGui_ImplDX11_InvalidateDeviceObjects();
        pFactory.Release();
        pd3dDevice.Release();
        pd3dDeviceContext.Release();
        // io.BackendRendererName = null;
        io.BackendRendererUserData = IntPtr.Zero;
        io.BackendFlags &= ~ImGuiBackendFlags.RendererHasVtxOffset;
        // TODO: Free memory when done
        // bd = null;
        // IM_DELETE(bd);
    }
    
    public unsafe void ImGui_ImplDX11_NewFrame()
    {
        //TODO
        // Debug.Assert(bd is not null, "Context or backend not initialized! Did you call ImGui_ImplDX11_Init()?");

        // TODO: Check if condition
        if ((long)pFontSampler.Handle == IntPtr.Zero)
            ImGui_ImplDX11_CreateDeviceObjects();
    }
    
    #endregion
    
    #region Structs

    struct VERTEX_CONSTANT_BUFFER_DX11
    {
        public Matrix4X4<float> mvp;
    }
    
    // Backup DX state that will be modified to restore it afterward (unfortunately this is very ugly looking and verbose. Close your eyes!)
    // I added some pointers to pass arrays of instances where I thought it was needed
    unsafe struct BACKUP_DX11_STATE
    {
        public uint                        ScissorRectsCount, ViewportsCount;
        public Box2D<int>*                 ScissorRects;
        public Viewport*                   Viewports;
        public ID3D11RasterizerState*      RS;
        public ID3D11BlendState*           BlendState;
        public float*                      BlendFactor;
        public uint                        SampleMask;
        public uint                        StencilRef;
        public ID3D11DepthStencilState*    DepthStencilState;
        public ID3D11ShaderResourceView*   PSShaderResource;
        public ID3D11SamplerState*         PSSampler;
        public ID3D11PixelShader*          PS;
        public ID3D11VertexShader*         VS;
        public ID3D11GeometryShader*       GS;
        public uint                        PSInstancesCount, VSInstancesCount, GSInstancesCount;
        public ComPtr<ID3D11ClassInstance> PSInstances, VSInstances, GSInstances;   // 256 is max according to PSSetShader documentation
        public D3DPrimitiveTopology        PrimitiveTopology;
        public ID3D11Buffer*               IndexBuffer, VertexBuffer, VSConstantBuffer;
        public uint                        IndexBufferOffset, VertexBufferStride, VertexBufferOffset;
        public Format                      IndexBufferFormat;
        public ID3D11InputLayout*          InputLayout;
    };
    #endregion

}