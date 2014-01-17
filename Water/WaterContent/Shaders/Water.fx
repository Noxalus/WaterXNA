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
float TextureScale = 200.5f;

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


struct VertexShaderInput
{
    float4 Position : POSITION0;
	float2 UV  		: TEXCOORD0;
	float3 Normal 	: NORMAL;
};

struct VertexShaderOutput
{
	float4 Position					: POSITION0;
	float3 ToEyeWorld				: TEXCOORD0;
	float2 WaveNormalMapPosition0	: TEXCOORD1;
	float2 WaveNormalMapPosition1	: TEXCOORD2;
	float4 RefractionPosition		: TEXCOORD3;
	float4 ReflectionPosition		: TEXCOORD4;
	float4 Test						: TEXCOORD5;

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

	output.ToEyeWorld = output.Position - CameraPosition;

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

	// Scroll texture coordinates.
	output.WaveNormalMapPosition0 = (input.UV * TextureScale) + WaveMapOffset0;
	output.WaveNormalMapPosition1 = (input.UV * TextureScale) + WaveMapOffset1;

	output.Test = output.Position;

    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
	float2 refractionTexCoord;
	float4 refractionColor;
	float2 reflectionTexCoord;
	float4 reflectionColor;
	float4 color;


	float4 WaterColor = float4(0.5f, 0.79f, 0.75f, 1.0f);
	float4 SunColor = float4(1.0f, 0.8f, 0.4f, 1.0f);
	float3 SunDirection = float3(2.6f, -1.0f, -1.5f);
	float SunFactor = 1.5f;
	float SunPower = 250.0f;
	
	// Light vector is opposite the direction of the light.
	float3 lightVecW = -SunDirection;
	float4 projTexC;

	//transform the projective texcoords to NDC space
	//and scale and offset xy to correctly sample a DX texture
	projTexC = input.Test;
	projTexC.xyz /= projTexC.w;
	projTexC.xyz /= projTexC.w;
	projTexC.x = 0.5f*projTexC.x + 0.5f;
	projTexC.y = -0.5f*projTexC.y + 0.5f;
	projTexC.z = .1f / projTexC.z; //refract more based on distance from the camera
	
	input.ToEyeWorld = normalize(input.ToEyeWorld);

	// Sample normal map.
	float3 normalT0 = tex2D(SampleTypeWaveNormalMap0, input.WaveNormalMapPosition0);
	float3 normalT1 = tex2D(SampleTypeWaveNormalMap1, input.WaveNormalMapPosition1);

	//unroll the normals retrieved from the normalmaps
	normalT0.yz = normalT0.zy;
	normalT1.yz = normalT1.zy;

	normalT0 = 2.0f * normalT0 - 1.0f;
	normalT1 = 2.0f * normalT1 - 1.0f;

	float3 normalT = normalize(0.5f * (normalT0 + normalT1));
	float3 n1 = float3(0, 1, 0); //we'll just use the y unit vector for spec reflection.

	//get the reflection vector from the eye
	float3 R = normalize(reflect(input.ToEyeWorld, normalT));

	float4 finalColor;
	finalColor.a = 1;

	//compute the fresnel term to blend reflection and refraction maps
	float ang = saturate(dot(-input.ToEyeWorld, n1));
	float f = R0 + (1.0f - R0) * pow(1.0f - ang, 5.0);

	//also blend based on distance
	f = min(1.0f, f + 0.007f * CameraPosition.y);

	//compute the reflection from sunlight
	float sunFactor = SunFactor;
	float sunPower = SunPower;

	if (CameraPosition.y < input.Test.y)
	{
		sunFactor = 7.0f; //these could also be sent to the shader
		sunPower = 55.0f;
	}

	float3 sunlight = sunFactor * pow(saturate(dot(R, lightVecW)), sunPower) * SunColor;

	//refractionColor = tex2D(SampleTypeRefraction, projTexC.xy - projTexC.z/* * normalT.xz*/);
	//reflectionColor = tex2D(SampleTypeReflection, projTexC.xy + projTexC.z/* * normalT.xz*/);


	// Calculate the projected refraction texture coordinates.
	refractionTexCoord.x = (input.RefractionPosition.x / input.RefractionPosition.w) / 2.0f + 0.5f;
	refractionTexCoord.y = (-input.RefractionPosition.y / input.RefractionPosition.w) / 2.0f + 0.5f;

	// Calculate the projected reflection texture coordinates.
	reflectionTexCoord.x = input.ReflectionPosition.x / input.ReflectionPosition.w / 2.0f + 0.5f;
	reflectionTexCoord.y = -input.ReflectionPosition.y / input.ReflectionPosition.w / 2.0f + 0.5f;

	// Sample the texture pixels from the textures using the updated texture coordinates.
	refractionColor = tex2D(SampleTypeRefraction, refractionTexCoord + normalT.xz);
	reflectionColor = tex2D(SampleTypeReflection, reflectionTexCoord + normalT.xz);

	//only use the refraction map if we're under water
	if (CameraPosition.y < input.Test.y)
		f = 0.0f;

	//interpolate the reflection and refraction maps based on the fresnel term and add the sunlight
	finalColor.rgb = WaterColor * lerp(refractionColor, reflectionColor, f); //+ sunlight;

	return finalColor;
}

technique ClassicTechnique
{
    pass Pass1
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}
