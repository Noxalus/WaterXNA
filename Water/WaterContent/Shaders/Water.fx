float4x4 World;
float4x4 View;
float4x4 Projection;

float3 CameraPosition;

matrix ReflectionMatrix;
Texture2D RefractionTexture;
Texture2D ReflectionTexture;

// Normal maps
Texture2D WaveNormalMap0;
Texture2D WaveNormalMap1;

// Texture coordinate offset vectors for scrolling
// normal maps.
float2 WaveMapOffset0;
float2 WaveMapOffset1;

//scale used on the wave maps
float WaveTextureScale;

bool EnableWaves;
bool EnableRefraction;
bool EnableReflection;
bool EnableFresnel;
bool EnableSpecularLighting;

float4 WaterColor;

// Sun
float4 SunColor;
float3 SunDirection;
float SunFactor;
float SunPower;

float RefractionReflectionMergeTerm;

// Constant for Fresnel computation
static const float R0 = 0.02037f;

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

sampler2D SampleTypeWaveNormalMap0 = sampler_state
{
	Texture = <WaveNormalMap0>;
	MinFilter = LINEAR;
	MagFilter = LINEAR;
	MipFilter = LINEAR;
	AddressU = WRAP;
	AddressV = WRAP;
};

sampler2D SampleTypeWaveNormalMap1 = sampler_state
{
	Texture = <WaveNormalMap1>;
	MinFilter = LINEAR;
	MagFilter = LINEAR;
	MipFilter = LINEAR;
	AddressU = WRAP;
	AddressV = WRAP;
};

// Function calculating fresnel term.
float ComputeFresnelTerm(float3 eyeVec, float3 cameraPosition)
{
	// We'll just use the y unit vector for spec reflection.
	float3 up = float3(0, 1, 0);

	// Compute the fresnel term to blend reflection and refraction maps
	float angle = saturate(dot(-eyeVec, up));
	float f = R0 + (1.0f - R0) * pow(1.0f - angle, 5.0);

	//also blend based on distance
	f = min(1.0f, f + 0.007f * cameraPosition.y);
	
	return f;
}

struct VertexShaderInput
{
	float4 Position : POSITION0;
	float2 UV  		: TEXCOORD0;
};

struct VertexShaderOutput
{
	float4 Position					: POSITION0;
	float3 ToCameraVector			: TEXCOORD0;
	float2 WaveNormalMapPosition0	: TEXCOORD1;
	float2 WaveNormalMapPosition1	: TEXCOORD2;
	float4 ReflectionPosition		: TEXCOORD3;
	float4 RefractionPosition   	: TEXCOORD4;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
	VertexShaderOutput output;
	matrix viewProjectWorld;
	matrix reflectProjectWorld;

	// Change the position vector to be 4 units for proper matrix calculations.
	input.Position.w = 1.0f;

	float4x4 worldViewProj = mul(World, View);
	worldViewProj = mul(worldViewProj, Projection);

	output.Position = mul(input.Position, worldViewProj);

	output.ToCameraVector = input.Position - CameraPosition;

	// Calculate reflection position

	// Create the reflection projection world matrix.
	reflectProjectWorld = mul(ReflectionMatrix, Projection);
	reflectProjectWorld = mul(World, reflectProjectWorld);

	// Calculate the input position against the reflectProjectWorld matrix.
	output.ReflectionPosition = mul(input.Position, reflectProjectWorld);

	// Scroll texture coordinates.
	output.WaveNormalMapPosition0 = (input.UV * WaveTextureScale) + WaveMapOffset0;
	output.WaveNormalMapPosition1 = (input.UV * WaveTextureScale) + WaveMapOffset1;

	output.RefractionPosition = output.Position;

	return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
	// Light vector is the opposite of the light direction
	float3 lightVector = normalize(-SunDirection);
	float4 refractionTexCoord;
	float4 reflectionTexCoord;
	float4 refractionColor;
	float4 reflectionColor;
	float4 color;

	input.ToCameraVector = normalize(input.ToCameraVector);

	// Sample wave normal map
	float3 normalT0 = tex2D(SampleTypeWaveNormalMap0, input.WaveNormalMapPosition0);
	float3 normalT1 = tex2D(SampleTypeWaveNormalMap1, input.WaveNormalMapPosition1);

	// Unroll the normals retrieved from the normal maps
	normalT0.yz = normalT0.zy;
	normalT1.yz = normalT1.zy;

	normalT0 = 2.0f * normalT0 - 1.0f;
	normalT1 = 2.0f * normalT1 - 1.0f;

	float3 normalT = normalize(0.5f * (normalT0 + normalT1));

	float fresnelTerm = ComputeFresnelTerm(input.ToCameraVector, CameraPosition);

	// Compute the reflection from sunlight

	// Get the reflection vector from the eye
	float3 R;
	float3 up = float3(0, 1.0f, 0);
	float3 sunLight = 0;

	if (EnableWaves)
		R = normalize(reflect(input.ToCameraVector, normalT));
	else
		R = normalize(reflect(input.ToCameraVector, up));

	sunLight = SunFactor * pow(saturate(dot(R, lightVector)), SunPower) * SunColor;
	
	if (!EnableFresnel)
		fresnelTerm = RefractionReflectionMergeTerm;

	// Transform the projective refraction texcoords to NDC space
	// and scale and offset xy to correctly sample a DX texture
	refractionTexCoord = input.RefractionPosition;
	refractionTexCoord.xyz /= refractionTexCoord.w;
	refractionTexCoord.x = 0.5f * refractionTexCoord.x + 0.5f;
	refractionTexCoord.y = -0.5f * refractionTexCoord.y + 0.5f;
	// refract more based on distance from the camera
	refractionTexCoord.z = .1f / refractionTexCoord.z; 

	// Transform the projective reflection texcoords to NDC space
	// and scale and offset xy to correctly sample a DX texture
	reflectionTexCoord = input.ReflectionPosition;
	reflectionTexCoord.xyz /= reflectionTexCoord.w;
	reflectionTexCoord.x = 0.5f * reflectionTexCoord.x + 0.5f;
	reflectionTexCoord.y = -0.5f * reflectionTexCoord.y + 0.5f;
	// reflect more based on distance from the camera
	reflectionTexCoord.z = .1f / reflectionTexCoord.z;

	// Sample refraction & reflection
	if (EnableWaves)
	{
		// Sample the texture pixels from the textures using the updated texture coordinates.
		refractionColor = tex2D(SampleTypeRefraction, refractionTexCoord.xy - refractionTexCoord.z * normalT.xz);
		reflectionColor = tex2D(SampleTypeReflection, reflectionTexCoord.xy + reflectionTexCoord.z * normalT.xz);
	}
	else
	{
		refractionColor = tex2D(SampleTypeRefraction, refractionTexCoord.xy);
		reflectionColor = tex2D(SampleTypeReflection, reflectionTexCoord.xy);
	}

	if (!EnableSpecularLighting)
		sunLight = 0;

	if (!EnableRefraction && !EnableReflection)
	{
		color.rgb = WaterColor + sunLight;
	}
	else
	{
		if (EnableRefraction && EnableReflection)
			color.rgb = WaterColor * lerp(refractionColor, reflectionColor, fresnelTerm) + sunLight;
		else if (EnableRefraction)
			color.rgb = WaterColor * refractionColor + sunLight;
		else
			color.rgb = WaterColor * reflectionColor + sunLight;
	}

	// alpha canal
	color.a = 1;

	return color;
}

technique ClassicTechnique
{
	pass Pass1
	{
		VertexShader = compile vs_3_0 VertexShaderFunction();
		PixelShader = compile ps_3_0 PixelShaderFunction();
	}
}
