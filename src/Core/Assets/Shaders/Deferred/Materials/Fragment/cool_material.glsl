//identifier=cool_material
//type=FragmentShader
//size=8

struct cool_material_struct { // 4 bytes
    int color1;
    int color2;
};

readonly layout(std430, set = FRAGMENT_MATERIAL_SET, binding = cool_material_binding) buffer cool_material_buffer {
    cool_material_struct cool_material_data[];
};

vec4 cool_material_f0(vec2 b, cool_material_struct mat) {
    float time = inUV.x * 50;
    b.x += sin(b.y * 3.0) / 15.0 * sin(0.75 + time / 3.5) * 1.3 + time / 28.0;
    b.y += sin(b.x * 4.0) / 3.0 - time / 40.0 + sin(time / 8.5);

    float val = mod(b.y + b.y, 0.2) * 7;

    vec4 col1 = intToRGBA(mat.color1);
    vec4 col2 = intToRGBA(mat.color2);

    vec4 a = mix(col1, col2, val);

    return a;
}

void cool_material(UiElementData data) {
    cool_material_struct mat = cool_material_data[data.fragmentDataIndex];
    vec2 p = vec2(0.001, 0.002);
    vec4 a = vec4(0);

    vec2 coords = fragCoord.xy;

    a += cool_material_f0(coords, mat);

    float samples = 8;
    for (float i = 0.0; i < PI * 2; i += PI * 2 / samples)
    {
        a += cool_material_f0(coords + vec2(sin(i), cos(i)) * p, mat);
    }

    outColor = a / (samples + 1);
}