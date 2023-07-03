#version 450
layout(location = 0) in vec3 inPosition;

layout(location = 0) out int textureId;

void main() {
	gl_Position = vec4(inPosition, 1.0f);
	textureId = gl_InstanceIndex;
}