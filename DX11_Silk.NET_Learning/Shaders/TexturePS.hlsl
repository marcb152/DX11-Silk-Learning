Texture2D tex: register(t1);

SamplerState splr;

cbuffer CBuff
{
    uint side_face;
    uint top_face;
    uint bottom_face;
}

float4 main(float2 texCoord : TexCoord, uint tid : SV_PrimitiveID) : SV_Target
{
    float2 offset;
    if (tid < 8)
    {
        offset = float2(side_face / 16.0f, 0.0f);
    }
    else if (tid < 10)
    {
        offset = float2(top_face / 16.0f, 0.0f);
    }
    else
    {
        offset = float2(bottom_face / 16.0f, 0.0f);
    }
    float2 editedUV = texCoord * 1.0f/16;
    
    return tex.Sample(splr, editedUV + offset);
}