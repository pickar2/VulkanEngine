//identifier=no_material
//type=FragmentShader
//size=0

struct no_material_struct { // 4 bytes
	int no_material_null;
};

readonly layout(std430, set = FRAGMENT_MATERIAL_SET, binding = no_material_binding) buffer no_material_buffer {
	no_material_struct no_material_data[];
};

void no_material(FragmentData fragData, MaterialData matData) {
	//    no_material_struct mat = no_material_data[data.fragmentDataIndex];
	//    outColor = vec4(0, 0, 0, 1);
}
