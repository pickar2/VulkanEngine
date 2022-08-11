//identifier=line_material
//type=VertexShader
//size=10

struct line_material_struct {
    int16_t startX;
    int16_t startY;

    int16_t endX;
    int16_t endY;

    int16_t size;
};

readonly layout(std430, set = VERTEX_MATERIAL_SET, binding = line_material_binding) buffer line_material_buffer {
    line_material_struct line_material_data[];
};

//const vec2[] vertexPos = { vec2(0, 0), vec2(1, 0), vec2(0, 1), vec2(1, 1) };

void line_material(UiElementData data) {
    line_material_struct mat = line_material_data[data.vertexDataIndex];

    const int index = gl_VertexIndex & 3;
    switch (index) {
        case 0:
        fragTexCoord = vec2(0, 0);
        break;
        case 1:
        fragTexCoord = vec2(1, 0.98);
        break;
        case 2:
        fragTexCoord = vec2(0, 0.02);
        break;
        case 3:
        fragTexCoord = vec2(1, 1);
        break;
    }
    gl_Position = proj * ortho * globalMatrix * modelMatrix * vec4(fragTexCoord, 0, 1.0);
}
