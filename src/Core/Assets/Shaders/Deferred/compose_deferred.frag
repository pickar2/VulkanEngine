#version 450

#extension GL_EXT_nonuniform_qualifier : enable
#extension GL_AMD_gpu_shader_int16 : enable 

#include "Default/constants.glsl"
#include "Default/structs.glsl"

layout (input_attachment_index = 0, binding = 0) uniform subpassInput samplerPosition;
layout (input_attachment_index = 1, binding = 1) uniform subpassInput samplerNormal;
layout (input_attachment_index = 2, binding = 2) uniform subpassInput samplerFragCoord;
layout (input_attachment_index = 3, binding = 3) uniform usubpassInput samplerMaterial;

layout (location = 0) in vec2 inUV;

layout (location = 0) out vec4 outColor;

#include "Default/functions.glsl"

vec3 fragPos;
vec3 normal;
vec4 fragCoord;
uvec4 material;

#include "@TestDeferredMaterialManager_fragment_includes.glsl"

void main() {
    fragPos = subpassLoad(samplerPosition).rgb;
    normal = subpassLoad(samplerNormal).rgb;
    fragCoord = subpassLoad(samplerFragCoord);
    material = uvec4(subpassLoad(samplerMaterial));

    outColor = vec4(0, 0, 0, 0.0);

    uint vertMat = material.y >> 8;
    uint fragMat = material.y & 0xffffu;

    UiElementData d;
    d.modelId = material.x;

    d.vertexMaterialType = vertMat;
    d.fragmentMaterialType = fragMat * (int(sin(inUV.y * 100) * cos(fragCoord.x * 10) + 2) % 2 + 1);

    d.vertexDataIndex = material.z;
    d.fragmentDataIndex = material.w;

    fragmentSwitch(d);

    outColor.a = 1.0;
}