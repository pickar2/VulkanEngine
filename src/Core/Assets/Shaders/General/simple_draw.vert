#version 450
layout(location = 0) in vec3 inPosition;

layout(location = 0) out int textureId;

void main() {
    vec3 offset = vec3(0, 0, 0);
    #include "testInclude.glsl"
    gl_Position = vec4(inPosition + offset, 1.0f);
    textureId = gl_InstanceIndex;
}