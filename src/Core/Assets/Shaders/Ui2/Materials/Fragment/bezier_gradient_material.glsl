//identifier=bezier_gradient_material
//type=FragmentShader
//size=12

struct bezier_gradient_material_struct { // 12 bytes
    int color1;
    int color2;
    float smoothing;
};

readonly layout(std430, set = FRAGMENT_MATERIAL_SET, binding = bezier_gradient_material_binding) buffer bezier_gradient_material_buffer {
    bezier_gradient_material_struct bezier_gradient_material_data[];
};

float bezier_gradient_material_smoothing(float smoothing, float x) {
    float t = max(max((smoothing-x)/smoothing, 0), max((x-1+smoothing)/smoothing, 0));
    return 1 - t * t * t;
    //	return 1 - t * t * (3 - 2 * t);
}

void bezier_gradient_material(UiElementData data) {
    bezier_gradient_material_struct mat = bezier_gradient_material_data[data.fragmentDataIndex];

    //	outColor = vec4(sin((floatData1.x+vec3(0, 2, 1) / 3.0) * acos(-1) * 2.0) * 0.5 + 0.5, 1); // `hsv` rainbow
    //	float alpha = max(smoothstep(0, mat.smoothing, floatData1.y), smoothstep(1-mat.smoothing, 1, floatData1.y));
    float alpha = bezier_gradient_material_smoothing(mat.smoothing, floatData1.y);
    outColor = vec4(mix(intToRGBA(mat.color1), intToRGBA(mat.color2), smoothstep(0, 1, floatData1.x)).xyz, alpha);
}
