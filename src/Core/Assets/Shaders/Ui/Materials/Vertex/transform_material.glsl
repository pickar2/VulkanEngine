//identifier=transform_material
//type=vertex
//size=64

struct transform_material_struct {
    mat4 transformMatrix;
};

readonly layout(std430, set = VERTEX_MATERIAL_SET, binding = transform_material_binding) buffer transform_material_buffer {
    transform_material_struct transform_material_data[];
};

void transform_material(UiElementData data) {
    transform_material_struct mat = transform_material_data[data.vertexDataIndex];
    fragTexCoord = vertexPos[gl_VertexIndex & 3];

    vec4 fullScreenCoord = globalMatrix * modelMatrix * mat.transformMatrix * vec4(fragTexCoord, 1.0, 1.0);

    fullScreenCoord.x /= fullScreenCoord.z;
    fullScreenCoord.y /= fullScreenCoord.z;
    fullScreenCoord.z = zIndex;

    screenCoord = fullScreenCoord.xy;
    gl_Position = proj * ortho * fullScreenCoord;
}
