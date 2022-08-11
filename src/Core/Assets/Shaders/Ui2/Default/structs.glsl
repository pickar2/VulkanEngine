// rectangle
struct UiElementData { // 60 bytes (aligned for 4 bytes)
    float baseX;
    float baseY;

    float localX;
    float localY;

    int16_t baseZ;
    int16_t localZ;

    float width;
    float height;

    float maskStartX;
    float maskStartY;

    float maskEndX;
    float maskEndY;

    int16_t vertexMaterialType;
    int16_t fragmentMaterialType;

    int vertexDataIndex;
    int fragmentDataIndex;

    int16_t rootIndex;
    int16_t flags;
};

// flags:
// 	int16_t rectangleBit   = 0x0001;
// 	int16_t triangleBit    = 0x0002;
// 	int16_t quadBit        = 0x0004;
//
// 	int16_t transformedBit = 0x0008;
//
// 	int16_t offscreenBit   = 0x2000;
// 	int16_t disabledBit    = 0x4000;
//struct UiRealElementData { // 40 bytes (aligned for 4 bytes)
//	int16_t flags;
//	int16_t z;
//	
//	int posIndex;
//	int transformationIndex;
//
//	float maskStartX;
//	float maskStartY;
//
//	float maskEndX;
//	float maskEndY;
//
//	int16_t vertexMaterialType;
//	int16_t fragmentMaterialType;
//
//	int vertexDataIndex;
//	int fragmentDataIndex;
//};
//
//struct RectanglePos { // 24 bytes (aligned for 8 bytes)
//	vec2 basePos;
//	vec2 localPos;
//	vec2 size;
//};
//
//struct TrianglePos { // 32 bytes (aligned for 8 bytes)
//	vec2 basePos;
//	vec2 p1, p2, p3;
//};
//
//struct QuadPos { // 40 bytes (aligned for 8 bytes)
//	vec2 basePos;
//	vec2 p1, p2, p3, p4;
//};
//
//struct TransformationMatrix { // 16 bytes (aligned for 16 bytes)
//	mat2 transform;
//};

struct Pos {
    float x;
    float y;

    int16_t z;
};
