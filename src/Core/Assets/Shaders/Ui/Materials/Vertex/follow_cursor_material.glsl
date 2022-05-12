//identifier=follow_cursor_material
//type=vertex
//size=4

readonly layout(std430, set = 3, binding = follow_cursor_material_binding) buffer follow_cursor_material_buffer {
    int follow_cursor_null;
};

void follow_cursor_material(UiElementData data) {
    fragTexCoord = vertexPos[gl_VertexIndex & 3];

    globalMatrix[3][0] += mousePos.x;
    globalMatrix[3][1] += mousePos.y;

    modelMatrix[3][0] -= data.width / 2;
    modelMatrix[3][1] -= data.height / 2;

    float rad = frameIndex * 3.14159 / 40;
    mat4 rotateZ = mat4(
    cos(rad), -sin(rad), 0, 0,
    sin(rad), cos(rad), 0, 0,
    0, 0, 1, 0,
    0, 0, 0, 1
    );

    gl_Position = proj * globalMatrix * rotateZ * modelMatrix * vec4(vertexPos[gl_VertexIndex & 3], 0, 1.0);
}
