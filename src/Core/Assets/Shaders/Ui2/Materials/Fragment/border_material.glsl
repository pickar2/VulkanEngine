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

float borderSDF(vec2 CenterPosition, vec2 Size, float Radius) {
	vec2 q = abs(CenterPosition-Size)-Size+Radius;
	return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - Radius;
}

void border_material(UiElementData data) {
	border_material_struct mat = border_material_data[data.fragmentDataIndex];

	Pos pos = calcFullPos(data);
	vec2 pixelPos = screenCoord.xy;

	float length = borderSDF(pixelPos - vec2(pos.x, pos.y), vec2(data.width, data.height) / 2.0, 0);
	if (length <= 0 && length >= -mat.size) {
		vec4 matColor = intToRGBA(mat.color);
		outColor = vec4(matColor.xyz, smoothstep(matColor.a, 0.0, (length / mat.size)));
	} else {
		outColor = vec4(0);
	}
}
