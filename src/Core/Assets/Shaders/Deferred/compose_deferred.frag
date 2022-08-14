#version 450

#extension GL_EXT_nonuniform_qualifier : enable
#extension GL_AMD_gpu_shader_int16 : enable 

layout (input_attachment_index = 0, binding = 0) uniform subpassInput samplerPosition;
layout (input_attachment_index = 1, binding = 1) uniform subpassInput samplerNormal;
layout (input_attachment_index = 2, binding = 2) uniform subpassInput samplerMaterial;

layout (location = 0) in vec2 inUV;

layout (location = 0) out vec4 outColor;

vec4 intToRGBA(int value) {
    float b = value & 0xFF;
    float g = (value >> 8) & 0xFF;
    float r = (value >> 16) & 0xFF;
    float a = (value >> 24) & 0xFF;

    return vec4(r, g, b, a) / 255.0;
}

void main() {
	vec3 fragPos = subpassLoad(samplerPosition).rgb;
	vec3 normal = subpassLoad(samplerNormal).rgb;
	uvec4 material = uvec4(subpassLoad(samplerMaterial));

	outColor = vec4(fragPos, 1.0);
}