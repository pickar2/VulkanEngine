﻿#version 450

layout(location = 0) out int textureId;

void main() {
	const vec3 positions[3] = vec3[3](
		vec3(+1, +1, 0),
		vec3(-1, +1, 0),
		vec3(+0, -1, 0)
	);

	gl_Position = vec4(positions[gl_VertexIndex], 1.0f);
	textureId = gl_InstanceIndex;
}