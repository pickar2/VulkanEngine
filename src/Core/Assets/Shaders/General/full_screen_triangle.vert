﻿#version 450

void main() {
    vec2 uv = vec2((gl_VertexIndex << 1) & 2, gl_VertexIndex & 2);
    gl_Position = vec4(uv * vec2(2, -2) + vec2(-1, 1), 0, 1);
}