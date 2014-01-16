float4x4 World;
float4x4 View;
float4x4 Projection;
Texture2D RefractionTexture;
Texture2D ReflectionTexture;
matrix ReflectionMatrix;

sampler2D SampleTypeRefraction = sampler_state
{
	Texture = <RefractionTexture>;
	MinFilter = LINEAR;
	MagFilter = LINEAR;
	MipFilter = LINEAR;
	AddressU = CLAMP;
	AddressV = CLAMP;
};

sampler2D SampleTypeReflection = sampler_state
{
	Texture = <ReflectionTexture>;
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
	float4 RefractionPosition : TEXCOORD3;
	float4 ReflectionPosition : TEXCOORD4;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;
	matrix viewProjectWorld;
	matrix reflectProjectWorld;

	// Change the position vector to be 4 units for proper matrix calculations.
	input.Position.w = 1.0f;

    float4 worldPosition = mul(input.Position, World);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);

	// Create the view projection world matrix for refraction.
	viewProjectWorld = mul(View, Projection);
	viewProjectWorld = mul(World, viewProjectWorld);

	// Calculate the input position against the viewProjectWorld matrix.
	float4x4 worldViewProj = mul(World, View);
	worldViewProj = mul(worldViewProj, Projection);
	output.RefractionPosition = mul(input.Position, worldViewProj);

	// Calculate reflection position

	// Create the reflection projection world matrix.
	reflectProjectWorld = mul(ReflectionMatrix, Projection);
	reflectProjectWorld = mul(World, reflectProjectWorld);

	// Calculate the input position against the reflectProjectWorld matrix.
	output.ReflectionPosition = mul(input.Position, reflectProjectWorld);

	output.Normal = input.Normal;

	output.UV = input.UV;

    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
	float2 refractionTexCoord;
	float4 refractionColor;
	float2 reflectionTexCoord;
	float4 reflectionColor;

	// Calculate the projected refraction texture coordinates.
	refractionTexCoord.x = (input.RefractionPosition.x / input.RefractionPosition.w) / 2.0f + 0.5f;
	refractionTexCoord.y = (-input.RefractionPosition.y / input.RefractionPosition.w) / 2.0f + 0.5f;

	// Calculate the projected reflection texture coordinates.
	reflectionTexCoord.x = input.ReflectionPosition.x / input.ReflectionPosition.w / 2.0f + 0.5f;
	reflectionTexCoord.y = -input.ReflectionPosition.y / input.ReflectionPosition.w / 2.0f + 0.5f;

	// Sample the texture pixels from the textures using the updated texture coordinates.
	refractionColor = tex2D(SampleTypeRefraction, refractionTexCoord);
	reflectionColor = tex2D(SampleTypeReflection, reflectionTexCoord);

	return lerp(refractionColor, reflectionColor, 0.6f);
}

technique ClassicTechnique
{
    pass Pass1
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}
