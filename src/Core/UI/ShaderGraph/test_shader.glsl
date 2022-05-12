#version 450

layout(location = 0) in vec3 fragColor;
layout(location = 1) in vec3 fragNormal;
layout(location = 2) in vec3 lightDirection;
layout(location = 3) in float distanceSquared;

layout(location = 0) out vec4 outColor;

const vec3 LightColor = vec3(0.4, 0.4, 0.2);
const float LightPower = 1000;

void main() {
    float cosTheta = clamp(dot(fragNormal, lightDirection), 0, 1);

    outColor = vec4(fragColor * LightColor * LightPower * cosTheta * distanceSquared, 1);
}
