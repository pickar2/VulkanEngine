//identifier=big_gradient_material
//type=FragmentShader
//size=16

struct big_gradient_material_struct { // 16 bytes (aligned for 4 bytes)
	int color1;
	int color2;
	int16_t startX;
	int16_t startY;
	int16_t endX;
	int16_t endY;
};

readonly layout(std430, set = FRAGMENT_MATERIAL_SET, binding = big_gradient_material_binding) buffer big_gradient_material_buffer {
	big_gradient_material_struct big_gradient_material_data[];
};

void big_gradient_material(UiElementData data) {
	big_gradient_material_struct mat = big_gradient_material_data[data.fragmentDataIndex];
	Pos pos = calcFullPos(data);
	vec4 col1 = intToRGBA(mat.color1);
	vec4 col2 = intToRGBA(mat.color2);

	float value = float(pos.y + fragTexCoord.y * data.height - mat.startY) / (mat.endY - mat.startY);

	outColor = mix(col1, col2, smoothstep(0, 1, value));
}
