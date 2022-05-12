//identifier=colored_texture_material
//type=fragment
//size=8

struct colored_texture_material_struct { // 8 bytes (aligned for 4 bytes)
	int color;
	int textureId;
};

readonly layout(std430, set = 4, binding = colored_texture_material_binding) buffer colored_texture_material_buffer {
	colored_texture_material_struct colored_texture_material_data[];
};

void colored_texture_material(UiElementData data) {
	colored_texture_material_struct mat = colored_texture_material_data[data.fragmentDataIndex];
	outColor = intToRGBA(mat.color);
	outColor *= texture(textures[mat.textureId], fragTexCoord);
}
