#version 450

#extension GL_EXT_nonuniform_qualifier : enable
#extension GL_AMD_gpu_shader_int16 : enable 

#include "Default/constants.glsl"
#include "Default/structs.glsl"

#include "Generated/global_data_includes.glsl"

layout (set = 1, input_attachment_index = 0, binding = 0) uniform subpassInput samplerPosition;
layout (set = 1, input_attachment_index = 1, binding = 1) uniform subpassInput samplerNormal;
layout (set = 1, input_attachment_index = 2, binding = 2) uniform subpassInput samplerFragCoord;
layout (set = 1, input_attachment_index = 3, binding = 3) uniform usubpassInput samplerMaterial;

layout (location = 0) in vec2 inUV;

layout (location = 0) out vec4 outColor;

layout(set = TEXTURE_SET, binding = 0) uniform sampler2D textures[];

//layout(set = MATERIAL_INDICES_SET, binding = 0) buffer material_indices_buffer {
//    uint[] material_indices;
//};

#include "Default/functions.glsl"

#include "@TestDeferredMaterialManager_fragment_includes.glsl"

void main() {
	FragmentData fragData;
	MaterialData matData;

	vec4 subpassPosition = subpassLoad(samplerPosition);
	fragData.pos = subpassPosition.xyz;
	fragData.linearDepth = subpassPosition.w;

	vec4 subpassNormal = subpassLoad(samplerNormal);
	fragData.normal = subpassNormal.xyz;
	//    fragData.null = samplerNormal.w;

	fragData.fragCoord = subpassLoad(samplerFragCoord);

	uvec4 material = subpassLoad(samplerMaterial);

	outColor = vec4(0, 0, 0, 0.0);

	uint vertMat = material.y >> 8;
	uint fragMat = material.y & 0xffffu;

	matData.modelId = material.x;

	matData.vertexMaterialType = vertMat;
//	matData.fragmentMaterialType = fragMat * (int(sin(inUV.y * 100) * cos(fragData.fragCoord.x * 10) + 2) % 2 + 1);
	matData.fragmentMaterialType = fragMat;

	matData.vertexDataIndex = material.z;
	matData.fragmentDataIndex = material.w;

	fragmentSwitch(fragData, matData);

	//	if (inUV.x < 0.5) {
	//		if (inUV.y < 0.5) { // top left
	//			outColor.xyz = subpassPosition.xyz;
	//		} else { // bottom left
	//			outColor.xyz = subpassNormal.xyz;
	//		}
	//	} else {
	//		if (inUV.y < 0.5) { // top right
	//			outColor.xyz = vec3(vertMat / 255.0, matData.fragmentMaterialType / 255.0, matData.modelId / 2.0) * 100;
	//		} else { // bottom right
	//			// do nothing, draw as intended
	//		}
	//	}

	outColor.a = 1.0;
}