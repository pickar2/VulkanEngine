#version 450

#extension GL_EXT_nonuniform_qualifier : enable
#extension GL_AMD_gpu_shader_int16 : enable 

#include "Default/constants.glsl"
#include "Default/structs.glsl"

layout (input_attachment_index = 0, binding = 0) uniform subpassInput samplerPosition;
layout (input_attachment_index = 1, binding = 1) uniform subpassInput samplerNormal;
layout (input_attachment_index = 2, binding = 2) uniform usubpassInput samplerMaterial;

layout (location = 0) in vec2 inUV;

layout (location = 0) out vec4 outColor;

#include "Default/functions.glsl"

#include "@TestDeferredMaterialManager_fragment_includes.glsl"

void main() {
	vec3 fragPos = subpassLoad(samplerPosition).rgb;
	vec3 normal = subpassLoad(samplerNormal).rgb;
	uvec4 material = uvec4(subpassLoad(samplerMaterial));

	outColor = vec4(0, 0, 0, 1.0);

    uint16_t vertMat = uint16_t(material.y >> 8);
    uint16_t fragMat = uint16_t(material.y & 0xffffu);

    UiElementData d;
	d.modelId = material.x;

	d.vertexMaterialType = vertMat;
	d.fragmentMaterialType = fragMat;

	d.vertexDataIndex = material.z;
	d.fragmentDataIndex = material.w;

    fragmentSwitch(d);
}