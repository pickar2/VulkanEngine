#version 450

#extension GL_EXT_nonuniform_qualifier : enable
#extension GL_AMD_gpu_shader_int16 : enable 

layout(location = 0) flat in uint inModelId;
layout(location = 1) flat in uint inMaterialType;
layout(location = 2) flat in uvec2 inMaterialIndex;
layout(location = 3) in vec3 inPos;
layout(location = 4) in vec3 inNormal;
layout(location = 5) in vec2 inUV;

layout(location = 0) out vec4 outPosition;
layout(location = 1) out vec4 outNormal;
layout(location = 2) out uvec4 outMaterial;

const float NEAR_PLANE = 0.1f;
const float FAR_PLANE = 256.0f;

float linearDepth(float depth)
{
	float z = depth * 2.0f - 1.0f; 
	return (2.0f * NEAR_PLANE * FAR_PLANE) / (FAR_PLANE + NEAR_PLANE - z * (FAR_PLANE - NEAR_PLANE));	
}

void main() {
    outPosition = vec4(inPos, linearDepth(gl_FragCoord.z));

	vec3 N = normalize(inNormal);
	N.y = -N.y;
	outNormal = vec4(N, 1.0);

    outMaterial = uvec4(inMaterialType, inMaterialIndex, inModelId);
}