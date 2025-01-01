cbuffer CBuf
{
    matrix transform;
}

struct VSOutput
{
    float2 texCoord : TexCoord;
    float4 pos : SV_Position;
};

VSOutput main(float3 pos : Position, float2 tex : TexCoord)
{
    VSOutput output;
    output.pos = mul(float4(pos, 1.0f), transform);
    output.texCoord = tex;
    return output;
}