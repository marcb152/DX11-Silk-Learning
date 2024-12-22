struct VSOut
{
	float4 pos : SV_Position;
	float4 color : Color;
};

cbuffer Cbuffer
{
	column_major matrix transform;
}

VSOut main( float3 pos : Position, float4 color : Color )
{
	VSOut output;
	output.pos = mul(float4(pos, 1.0f), transform);
	output.color = color;
	return output;
}