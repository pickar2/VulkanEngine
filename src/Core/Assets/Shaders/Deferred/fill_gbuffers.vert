#version 450

#extension GL_EXT_nonuniform_qualifier : enable
#extension GL_AMD_gpu_shader_int16 : enable 

layout(location = 0) in uint inModelId;
layout(location = 1) in uint inMaterialType;
layout(location = 2) in uvec2 inMaterialIndex;
layout(location = 3) in vec3 inPos;
layout(location = 4) in vec3 inNormal;
layout(location = 5) in vec2 inUV;

layout(location = 0) out uint outModelId;
layout(location = 1) out uint outMaterialType;
layout(location = 2) out uvec2 outMaterialIndex;
layout(location = 3) out vec3 outPos;
layout(location = 4) out vec3 outNormal;
layout(location = 5) out vec2 outUV;

void main() {
    outModelId = inModelId;
    outMaterialType = inMaterialType;
    outMaterialIndex = inMaterialIndex;
    outPos = inPos;
    outNormal = inNormal;
    outUV = inUV;
}