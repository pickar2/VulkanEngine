//identifier=border_material
//type=FragmentShader
//size=8

struct border_material_struct { // 8 bytes (4 bytes padding)
	int color;
	float size;
};

readonly layout(std430, set = FRAGMENT_MATERIAL_SET, binding = border_material_binding) buffer border_material_buffer {
	border_material_struct border_material_data[];
};

void border_material(UiElementData data) {
	border_material_struct mat = border_material_data[data.fragmentDataIndex];

	vec2 quadSize = vec2(data.width, data.height);

	float borderSize = mat.size;
	vec2 pixelPos = floor(fragTexCoord * quadSize) + 0.5;

	if (pixelPos.x <= borderSize ||
	pixelPos.y <= borderSize ||
	pixelPos.x + 0.001 >= quadSize.x - borderSize ||
	pixelPos.y + 0.001 >= quadSize.y - borderSize
	) {
		outColor = vec4(intToRGBA(mat.color));
	} else {
		outColor = vec4(0);
	}
}
