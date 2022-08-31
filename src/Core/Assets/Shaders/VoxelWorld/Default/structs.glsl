//struct FragmentData {
//    vec3 pos;
//    float linearDepth;
//    vec3 normal;
////    float null;
//    vec4 fragCoord;
//};
//
//struct MaterialData { // 20 bytes (aligned for 4 bytes)
//    uint modelId;
//
//    uint vertexMaterialType;// can be uint16_t
//    uint fragmentMaterialType;// can be uint16_t
//
//    uint vertexDataIndex;
//    uint fragmentDataIndex;
//};

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
//
//struct Pos {
//    float x;
//    float y;
//
//    int16_t z;
//};
