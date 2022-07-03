//identifier=font_material
//type=fragment
//size=16

struct font_material_struct { // 16 bytes (aligned for 4 bytes)
    int textureId;
    float scale;
    int color;
    float outlineDistance;
};

readonly layout(std430, set = FRAGMENT_MATERIAL_SET, binding = font_material_binding) buffer font_material_buffer {
    font_material_struct font_material_data[];
};

const vec4 outlineColor = vec4(0, 0, 0, 1);

void font_material(UiElementData data) {
    font_material_struct mat = font_material_data[data.fragmentDataIndex];
    float smoothing = 0.25 / (4 * mat.scale);
    float distance = texture(textures[mat.textureId], fragTexCoord).a;
    float outlineFactor = smoothstep(0.5 - smoothing, 0.5 + smoothing, distance);
    vec4 color = mix(outlineColor, intToRGBA(mat.color), outlineFactor);
    float alpha = smoothstep(mat.outlineDistance - smoothing, mat.outlineDistance + smoothing, distance);
    outColor = vec4(color.rgb, color.a * alpha);
}
