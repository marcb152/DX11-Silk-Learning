Texture2D tex;

SamplerState splr;

float4 main(float2 texCoord : TexCoord) : SV_Target
{
    float2 editedUV = texCoord * 1.0f/16;
    
    return tex.Sample(splr, editedUV);
}