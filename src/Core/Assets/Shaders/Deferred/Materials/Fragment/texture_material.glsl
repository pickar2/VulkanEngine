//identifier=texture_material
//type=FragmentShader
//size=4

struct texture_material_struct { // 4 bytes
    int textureId;
};

readonly layout(std430, set = FRAGMENT_MATERIAL_SET, binding = texture_material_binding) buffer texture_material_buffer {
    texture_material_struct texture_material_data[];
};

void texture_material(UiElementData data) {
    texture_material_struct mat = texture_material_data[data.fragmentDataIndex];
    outColor = texture(textures[mat.textureId], fragCoord.xy);
}
