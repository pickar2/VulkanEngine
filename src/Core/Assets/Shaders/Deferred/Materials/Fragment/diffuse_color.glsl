//identifier=color_material
//type=FragmentShader
//size=4

struct color_material_struct { // 4 bytes
	int color;
};

readonly layout(std430, set = FRAGMENT_MATERIAL_SET, binding = color_material_binding) buffer color_material_buffer {
	color_material_struct color_material_data[];
};

void color_material(FragmentData fragData, MaterialData matData) {
	color_material_struct mat = color_material_data[matData.fragmentDataIndex];
	outColor = intToRGBA(mat.color) * vec4(fragData.fragCoord.xy, inUV.y, 1);
}
