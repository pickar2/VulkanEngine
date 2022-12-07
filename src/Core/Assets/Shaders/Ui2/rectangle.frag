#version 460

#extension GL_EXT_nonuniform_qualifier : enable
//#extension GL_AMD_gpu_shader_int16 : enable
#extension GL_EXT_shader_explicit_arithmetic_types : enable

#include "Default/constants.glsl"
#include "Default/structs.glsl"

layout(location = 0) in flat int componentIndex;
layout(location = 1) in vec2 screenCoord;
layout(location = 2) in vec2 fragTexCoord;
layout(location = 3) in flat ivec4 intData;
layout(location = 4) in vec4 floatData1;
//layout(location = 4) in vec4 floatData2;

layout(location = 0) out vec4 outColor;

layout(set = TEXTURE_SET, binding = 0) uniform sampler2D textures[];

readonly layout(std430, set = ELEMENT_DATA_SET, binding = 0) buffer dataArray {
    UiElementData data[];
};

#include "Default/functions.glsl"

#include "Generated/global_data_includes.glsl"

#include "@Mat1_fragment_includes.glsl"

void main() {
    UiElementData d = data[componentIndex];

    Pos pos = calcFullPos(d);
    vec2 pixelPos = screenCoord.xy;
    if (!isPointInside(pixelPos, vec2(d.maskStartX, d.maskStartY), vec2(d.maskEndX, d.maskEndY))) {
        outColor = vec4(0);
        return;
    }

    fragmentSwitch(d);

    //    outColor.rgb = (gl_SampleMaskIn[0] >> 7) > 0 ? outColor.rgb : vec3(0);

    //    const float gamma = 2.2;
    //    outColor.rgb = pow(outColor.rgb, vec3(1.0/gamma));
}
