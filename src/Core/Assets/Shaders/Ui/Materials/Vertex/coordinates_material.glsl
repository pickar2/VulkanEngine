//identifier=coordinates_material
//type=vertex
//size=32

struct coordinates_material_struct {
    vec2[4] coords;
};

readonly layout(std430, set = 3, binding = coordinates_material_binding) buffer coordinates_material_buffer {
    coordinates_material_struct coordinates_material_data[];
};

void coordinates_material(UiElementData data) {
    coordinates_material_struct mat = coordinates_material_data[data.vertexDataIndex];
    fragTexCoord = mat.coords[gl_VertexIndex & 3];
    gl_Position = proj * globalMatrix * modelMatrix * vec4(fragTexCoord, 0, 1.0);
}
