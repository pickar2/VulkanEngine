//identifier=default_vertex_material
//type=VertexShader
//size=0

readonly layout(std430, set = VERTEX_MATERIAL_SET, binding = default_vertex_material_binding) buffer default_vertex_material_buffer {
    int default_vertex_null;
};

void default_vertex_material(UiElementData data) {
    fragTexCoord = vertexPos[gl_VertexIndex & 3];

    vec4 fullScreenCoord = globalMatrix * modelMatrix * vec4(fragTexCoord, zIndex, 1.0);
    screenCoord = fullScreenCoord.xy;
    gl_Position = proj * ortho * fullScreenCoord;
}
