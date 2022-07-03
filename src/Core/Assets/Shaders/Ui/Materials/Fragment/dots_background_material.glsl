//identifier=dots_background_material
//type=fragment
//size=4

struct dots_background_material_struct { // 4 bytes
    float scale;
};

readonly layout(std430, set = FRAGMENT_MATERIAL_SET, binding = dots_background_material_binding) buffer dots_background_material_buffer {
	dots_background_material_struct dots_background_material_data[];
};

void dots_background_material(UiElementData data) {
    dots_background_material_struct mat = dots_background_material_data[data.fragmentDataIndex];

    float time = frameIndex / 25.0;
    vec2 canvasSize = vec2(data.width, data.height);
    vec2 mPos = mousePos;

    float scale = mat.scale;

    int brightSize = 5;
    float squareSize = 5 * scale;
    float spacing = 30.0 * scale;

    vec2 offset = vec2(spacing / 2);//vec2(sin(time*7.0/11.0) * 100.0, sin(time*5.0/11.0) * 70.0);

    vec2 pixelCoord = fragTexCoord * canvasSize;
    pixelCoord += offset;
    mPos += offset;

    vec3 bgColor = vec3(0.03);
    vec3 dotColor = vec3(0.85, 0.2, 0.2);
    vec3 otherColor = vec3(0.6, 0.6, 0.85);

    vec2 dotNumber = floor(pixelCoord / spacing);
    vec2 dotStart = dotNumber * spacing + spacing / 2.0;

    float sizeVariance = 1.0;
    float size = 2.0 + ((sin(length(dotNumber) + time) + 1.0) / 2.0) * sizeVariance;
    size *= scale;

    float other = float(mod(dotNumber.x, squareSize) == 0 && mod(dotNumber.y, squareSize) == 0);

    float distance = clamp(length(pixelCoord - dotStart) / size, 0.0, 1.0);
    vec4 col = vec4(mix(mix(dotColor, otherColor, other), bgColor, distance), 1.0);

    float higlightDistance = 100.0;
    float higlightStrength = 2.0;
    float distToMouse = 1.0 + higlightStrength - clamp(length(mPos - pixelCoord), 0.0, higlightDistance) / higlightDistance * higlightStrength;

    float lineThickness = 1.0;
    vec2 lineDist = abs(dotStart - pixelCoord);
    float line = float(lineDist.x <= lineThickness || lineDist.y <= lineThickness);

    col += 0.01 * line * distToMouse;

    vec2 brightDotStart = floor(dotNumber / brightSize) * brightSize * spacing + spacing / 2.0;
    vec2 brightLineDist = abs(brightDotStart - pixelCoord);
    float brightLine = float(brightLineDist.x <= lineThickness || brightLineDist.y <= lineThickness);

    col += 0.015 * brightLine * distToMouse;

    outColor = col;
}
