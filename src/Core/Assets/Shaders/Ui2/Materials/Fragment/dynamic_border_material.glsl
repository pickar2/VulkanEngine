//identifier=dynamic_border_material
//type=FragmentShader
//size=16

struct dynamic_border_material_struct {
	int borderColor;
	int selectColor;
	int16_t selectRadius;
	int16_t rounding;
	int16_t borderThickness;
	int16_t null;
};

readonly layout(std430, set = FRAGMENT_MATERIAL_SET, binding = dynamic_border_material_binding) buffer dynamic_border_material_buffer {
	dynamic_border_material_struct dynamic_border_material_data[];
};

float roundedBoxSDF(vec2 CenterPosition, vec2 Size, float Radius) {
	vec2 q = abs(CenterPosition-Size)-Size+Radius;
	return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - Radius;
}

void dynamic_border_material(UiElementData data) {
	dynamic_border_material_struct mat = dynamic_border_material_data[data.fragmentDataIndex];
	Pos pos = calcFullPos(data);

	vec2 size = vec2(data.width, data.height);
	vec2 location = vec2(pos.x, pos.y) + 3;
	float thickness = 1.0;
	float shadowSoftness = 1.5f;
	vec2 shadowOffset = vec2(0.0, 0.0);
	float edgeSoftness = 4.0;
	float radius = 80.0;

	float innerShadowRadius = 10.0;
	float activationDistance = 75.0;

	vec2 pixelPos = fragTexCoord * size - 3;
	size -= 6;

	float distanceToCursor = length(location + pixelPos - mousePos);

	float distance = roundedBoxSDF(pixelPos, size/2.0, radius/2.0);
	float smoothedAlpha;
	if (distance <= -innerShadowRadius) {
		smoothedAlpha = 0.0;
	} else if (distance <= 0.0) {
		if (roundedBoxSDF(location+size-mousePos.xy, size/2.0, radius/2.0) < 0.0) {
			smoothedAlpha = (1.0 - smoothstep(0.0, innerShadowRadius*1.3, -distance))/1.5;
		} else {
			if (distanceToCursor < activationDistance) {
				smoothedAlpha = 1.0-distanceToCursor / activationDistance;
				smoothedAlpha *= (1.0 - smoothstep(0.0, innerShadowRadius*1.3, -distance))/1.5;
			} else {
				smoothedAlpha = 0.0;
			}
		}
	} else {
		smoothedAlpha = 1.0 - smoothstep(-edgeSoftness, edgeSoftness, abs(distance)-thickness);
	}

	vec4 quadColor = mix(vec4(0.66, 0.66, 0.66, 1.0), vec4(intToRGBA(mat.borderColor).rgb, smoothedAlpha), smoothedAlpha);

	float shadowDistance = roundedBoxSDF(pixelPos + shadowOffset, size/2.0, radius/2.0);
	float shadowAlpha = 1.0 - smoothstep(-shadowSoftness/2.0, shadowSoftness/2.0, abs(shadowDistance));
	vec4 shadowColor = vec4(0.6, 0.6, 0.6, 1.0);
	outColor = mix(quadColor, shadowColor, shadowAlpha - smoothedAlpha);
}
