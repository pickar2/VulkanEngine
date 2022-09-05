#version 450

layout (location = 0) out vec3 outPos;

#include "Default/constants.glsl"
#include "Default/structs.glsl"

readonly layout(std430, set = SCENE_DATA_SET, binding = 5) buffer sceneDataDescriptor {
	vec3 localCameraPos;
	float pad0;
	ivec3 cameraChunkPos;
	int frameIndex;
	vec3 viewDirection;
	float pad2;
	mat4 viewMatrix;
	mat4 projMatrix;
};

#define CHUNK_SIZE 8

const int[] components = {0, 0, 1, 1, 2, 2};
const float spread = +0.0001f;
const float[] moves = {CHUNK_SIZE + spread, -spread};

void main() {
	int x = gl_VertexIndex & 0xff;
	int y = (gl_VertexIndex >> 8) & 0xff;
	int z = (gl_VertexIndex >> 16) & 0xff;
	int id = (gl_VertexIndex >> 24) & 0xff;

	vec3 pos = vec3(x, y, z) * CHUNK_SIZE;

	int sideId = id / 4;

	int comp0 = components[sideId];
	int comp1 = (comp0 + 1) % 3;
	int comp2 = (comp0 + 2) % 3;

	int vertexId = id & 3;
	int flip = (sideId & 1);

	pos[comp0] += flip * (CHUNK_SIZE + spread) + (1 - flip) * (-spread);

	int move1 = vertexId / 2;
	int move2 = vertexId & 1;

	pos[comp1] += moves[move1];
	pos[comp2] += moves[move2];

    gl_Position = projMatrix * viewMatrix * vec4(pos, 1.0);
	outPos = pos;
}