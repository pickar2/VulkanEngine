//identifier=pixel_coordinates_material
//type=vertex
//size=64

struct pixel_coordinates_material_struct {
    vec4[4] coords;
};

readonly layout(std430, set = 3, binding = pixel_coordinates_material_binding) buffer pixel_coordinates_material_buffer {
    pixel_coordinates_material_struct pixel_coordinates_material_data[];
};

void pixel_coordinates_material(UiElementData data) {
    pixel_coordinates_material_struct mat = pixel_coordinates_material_data[data.vertexDataIndex];
    vec4 matCoords = mat.coords[gl_VertexIndex & 3];
    floatData1.xy = matCoords.zw;
    fragTexCoord = vec2(matCoords.x / data.width, matCoords.y / data.height);

    vec4 screenCoord = globalMatrix * modelMatrix * vec4(fragTexCoord, zIndex, 1.0);
    fragCoord = screenCoord.xy;
    gl_Position = proj * screenCoord;
}
