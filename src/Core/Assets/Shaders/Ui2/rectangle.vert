#version 460

#extension GL_EXT_nonuniform_qualifier : enable
#extension GL_AMD_gpu_shader_int16 : enable 

#include "Default/constants.glsl"
#include "Default/structs.glsl"

layout(location = 0) in int index;

layout(location = 0) out int componentIndex;
layout(location = 1) out vec2 screenCoord;
layout(location = 2) out vec2 fragTexCoord;
layout(location = 3) out ivec4 intData;
layout(location = 4) out vec4 floatData1;
//layout(location = 5) out vec4 floatData2;

readonly layout(std430, set = ELEMENT_DATA_SET, binding = 0) buffer dataArray {
	UiElementData data[];
};

#include "Default/functions.glsl"

#include "Generated/global_data_includes.glsl"

const vec2[] vertexPos = { vec2(0, 0), vec2(1, 0), vec2(0, 1), vec2(1, 1) };

mat4 modelMatrix = mat4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);
mat4 globalMatrix = mat4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);
float zIndex = 0;

#include "@Mat1_vertex_includes.glsl"

void main() {
	intData = ivec4(0);
	floatData1 = vec4(0);
	//	floatData2 = vec4(0);

	componentIndex = index;
	UiElementData d = data[index];

	Pos pos = calcFullPos(d);
	screenCoord = vec2(pos.x, pos.y) + vertexPos[gl_VertexIndex & 3] * vec2(d.width, d.height);

	modelMatrix[0][0] = d.width;
	modelMatrix[1][1] = d.height;

	modelMatrix[3][0] = d.localX;
	modelMatrix[3][1] = d.localY;

	globalMatrix[3][0] = d.baseX;
	globalMatrix[3][1] = d.baseY;
	zIndex = (d.localZ + d.baseZ) / MAX_Z_INDEX;

	vertexSwitch(d);
}
