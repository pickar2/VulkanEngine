//identifier=texture_uv_material
//type=VertexShader
//size=32

struct texture_uv_material_struct {
    vec2[4] uv;
};

readonly layout(std430, set = VERTEX_MATERIAL_SET, binding = texture_uv_material_binding) buffer texture_uv_material_buffer {
    texture_uv_material_struct texture_uv_material_data[];
};

void texture_uv_material(UiElementData data) {
    texture_uv_material_struct mat = texture_uv_material_data[data.vertexDataIndex];
    fragTexCoord = mat.uv[gl_VertexIndex & 3];

    vec4 fullScreenCoord = globalMatrix * modelMatrix * vec4(vertexPos[gl_VertexIndex & 3], zIndex, 1.0);
    screenCoord = fullScreenCoord.xy;
    gl_Position = proj * ortho * fullScreenCoord;
}
