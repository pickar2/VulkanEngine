vec4 intToRGBA(int value) {
    float b = value & 0xFF;
    float g = (value >> 8) & 0xFF;
    float r = (value >> 16) & 0xFF;
    float a = (value >> 24) & 0xFF;

    return vec4(r, g, b, a) / 255.0;
}

bool isPointInside(vec2 point, vec2 start, vec2 end) {
    return point.x >= start.x && point.y >= start.y && point.x < end.x && point.y < end.y;
}
