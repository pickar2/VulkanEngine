#version 450

#extension GL_EXT_nonuniform_qualifier : enable

layout(location = 0) in flat int textureId;

layout(location = 0) out vec4 outColor;

layout(set = 0, binding = 0) uniform sampler2D textures[];

void main() {
    outColor = texture(textures[textureId], gl_FragCoord.xy);
}
