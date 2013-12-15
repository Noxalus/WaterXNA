float4x4 World;
float4x4 View;
float4x4 Projection;
Texture2D Texture;

// General lighting
bool EnableLighting;

// Ambient lighting
float4 AmbientColor;
float AmbientIntensity;

// Diffuse lighting
float3 DiffuseLightDirection;
float4 DiffuseColor;
float DiffuseIntensity;

// Specular lighting
float Shininess;
float4 SpecularColor;
float SpecularIntensity;
float3 ViewVector;

sampler2D SampleType = sampler_state
{
	Texture = Texture;
	MinFilter = LINEAR;
	MagFilter = LINEAR;
	MipFilter = LINEAR;
	AddressU = WRAP;
	AddressV = WRAP;
};

struct VertexShaderInput
{
    float4 Position : POSITION0;
	float2 UV  		: TEXCOORD0;
	float3 Normal 	: NORMAL;

    // TODO: add input channels such as texture
    // coordinates and vertex colors here.
};

struct VertexShaderOutput
{
    float4 Position : POSITION0;
	float2 UV  		: TEXCOORD0;
	float3 Normal   : TEXCOORD1;

    // TODO: add vertex shader outputs such as colors and texture
    // coordinates here. These values will automatically be interpolated
    // over the triangle, and provided as input to your pixel shader.
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;

    float4 worldPosition = mul(input.Position, World);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);

	output.UV = input.UV;
	output.Normal = input.Normal;

    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
	float4 textureColor = tex2D(SampleType, input.UV);
	float4 color;

	if (EnableLighting)
	{
		float lightIntensity = dot(input.Normal, DiffuseLightDirection);
		color = saturate(DiffuseColor * DiffuseIntensity * lightIntensity);

		float3 light = normalize(DiffuseLightDirection);
		float3 normal = normalize(input.Normal);
		float3 r = normalize(2 * dot(light, normal) * normal - light);
		float3 v = normalize(mul(normalize(ViewVector), World));

		float dotProduct = dot(r, v);
		float4 specular = SpecularIntensity * SpecularColor * max(pow(dotProduct, Shininess), 0) * length(color);

		color = saturate(color * textureColor);
		color = saturate(color * (AmbientColor * AmbientIntensity) + specular);
	}
	else
		color = textureColor;

	return color;
}

technique ClassicTechnique
{
    pass Pass1
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}
