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
	const vec3 lightPos = vec3(0, -200, 0);
	const vec3 lightColor = vec3(0.96, 0.96, 0.86) - .2;

	const vec3 ambientColor = vec3(0.2);

	color_material_struct mat = color_material_data[matData.fragmentDataIndex];
	
	vec3 lightDir = normalize(lightPos - fragData.pos);
	
	float diff = max(dot(fragData.normal, lightDir), 0.0);
	vec3 diffuse = diff * lightColor;
	
	vec4 objectColor = intToRGBA(mat.color);

	outColor = vec4((ambientColor + diffuse) * objectColor.xyz, 1);
}
