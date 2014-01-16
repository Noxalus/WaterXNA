float4x4 World;
float4x4 View;
float4x4 Projection;
Texture2D Texture;
float4 ClippingPlane;

sampler2D SampleType = sampler_state
{
	Texture = <Texture>;
	MinFilter = LINEAR;
	MagFilter = LINEAR;
	MipFilter = LINEAR;
	AddressU = CLAMP;
	AddressV = CLAMP;
};

struct VertexShaderInput
{
    float4 Position : POSITION0;
	float2 UV  		: TEXCOORD0;
	float3 Normal 	: NORMAL;
};

struct VertexShaderOutput
{
    float4 Position           : POSITION0;
	float2 UV  		          : TEXCOORD0;
	float3 Normal		      : TEXCOORD1;
	float4 Clipping		      : TEXCOORD2;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;
	matrix viewProjectWorld;

	// Change the position vector to be 4 units for proper matrix calculations.
	input.Position.w = 1.0f;

    float4 worldPosition = mul(input.Position, World);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);

	output.Normal = input.Normal;

	output.UV = input.UV;

	// Clipping
	output.Clipping = dot(input.Position, ClippingPlane.xyz) + ClippingPlane.w;

    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
	clip(input.Clipping.x);

	float4 refr = tex2D(SampleType, input.UV);

	return refr;
}

technique ClassicTechnique
{
    pass Pass1
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}
