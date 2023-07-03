#version 450

layout(location = 0) out vec3 outColor;

vec4 intToRGBA(int value) {
	float b = value & 0xFF;
	float g = (value >> 8) & 0xFF;
	float r = (value >> 16) & 0xFF;
	float a = (value >> 24) & 0xFF;

	return vec4(r, g, b, a) / 255.0;
}

void main() {
	vec2 uv = vec2((gl_VertexIndex << 1) & 2, gl_VertexIndex & 2);
	gl_Position = vec4(uv * vec2(2, -2) + vec2(-1, 1), 0, 1);

	outColor = intToRGBA(gl_InstanceIndex).rgb;
}