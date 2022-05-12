//identifier=default_vertex_material
//type=vertex
//size=0

readonly layout(std430, set = 3, binding = default_vertex_material_binding) buffer default_vertex_material_buffer {
    int default_vertex_null;
};

void default_vertex_material(UiElementData data) {
    fragTexCoord = vertexPos[gl_VertexIndex & 3];

    vec4 screenCoord = globalMatrix * modelMatrix * vec4(fragTexCoord, zIndex, 1.0);
    fragCoord = screenCoord.xy;
    gl_Position = proj * screenCoord;
}
