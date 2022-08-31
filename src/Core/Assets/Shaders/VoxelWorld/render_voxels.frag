#version 450

#extension GL_EXT_nonuniform_qualifier : enable
#extension GL_AMD_gpu_shader_int16 : enable 

#include "Default/constants.glsl"
#include "Default/structs.glsl"

layout (location = 0) in vec2 inUV;

layout (location = 0) out vec4 outColor;

layout(set = TEXTURE_SET, binding = 0) uniform sampler2D textures[];

struct VoxelData {
	int16_t voxelTypeIndex;
	int16_t voxelMaterialType;
	int voxelMaterialIndex;
};

struct VoxelType {
	uint opaque;
//	int textureId;
//	float friction;
//	float density;
//	vec2 textureUv;
};

#define CHUNK_SIZE 16
#define CHUNK_SIZE_LOG2 4
#define CHUNK_VOXEL_COUNT CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE

#define VOXEL_MASK_BITCOUNT CHUNK_SIZE_LOG2
#define INT_BITCOUNT 32
#define MASK_COMPRESSION_LEVEL INT_BITCOUNT / VOXEL_MASK_BITCOUNT
#define MASK_COMPRESSION_LEVEL_LOG2 3// log2(8)

// 4 + 3 * 4 + CHUNK_VOXEL_COUNT / MASK_COMPRESSION_LEVEL * 4 + CHUNK_VOXEL_COUNT * 8 = 34832 bytes
struct ChunkData {
	int flags;// size: 4, offset: 0
	ivec3 chunkPos;// size: 12, offset: 4
	int[CHUNK_VOXEL_COUNT / MASK_COMPRESSION_LEVEL] mask;// size: 2048, offset: 16
//	VoxelData[CHUNK_VOXEL_COUNT] voxels;// size: 32768, offset: 2064
};

int GetVoxelIndex(int x, int y, int z) {
	return (z << (CHUNK_SIZE_LOG2 * 2)) | (y << CHUNK_SIZE_LOG2) | x;
}

int GetVoxelIndex(ivec3 pos) {
	return (pos.z << (CHUNK_SIZE_LOG2 * 2)) | (pos.y << CHUNK_SIZE_LOG2) | pos.x;
}

int GetVoxelMask(ChunkData chunk, int voxelIndex) {
	return (chunk.mask[voxelIndex >> MASK_COMPRESSION_LEVEL_LOG2] >> (VOXEL_MASK_BITCOUNT * (voxelIndex & (MASK_COMPRESSION_LEVEL - 1)))) & 0xF;
}

//#define SECTOR_SIZE 32
//#define SECTOR_SIZE_LOG2 5
//#define SECTOR_CHUNK_COUNT SECTOR_SIZE * SECTOR_SIZE * SECTOR_SIZE
//
//struct SectorData {
//	int[SECTOR_CHUNK_COUNT] chunkIndices;
//	ChunkData[] chunkData;
//};
//
//int GetChunkIndex(int x, int y, int z) {
//	return (z << (SECTOR_SIZE_LOG2 * 2)) | (y << SECTOR_SIZE_LOG2) | x;
//}
//
//int GetChunkIndex(ivec3 pos) {
//	return (pos.z << (SECTOR_SIZE_LOG2 * 2)) | (pos.y << SECTOR_SIZE_LOG2) | pos.x;
//}
//
//readonly layout(std430, set = SCENE_DATA_SET, binding = 0) buffer sectorInfoDescriptor {
//	int[] sectorIndices;
//};
//
//readonly layout(std430, set = SCENE_DATA_SET, binding = 1) buffer sectorDataDescriptor {
//	SectorData[] sectorData;
//};

readonly layout(std430, set = SCENE_DATA_SET, binding = 0) buffer chunkInfoDescriptor {
	int[] chunkIndices; // max array size = (chunk_render_distance)^3
};

readonly layout(std430, set = SCENE_DATA_SET, binding = 1) buffer chunkDataDescriptor {
	ChunkData[] chunkDataArray;
};

readonly layout(std430, set = SCENE_DATA_SET, binding = 2) buffer chunkVoxelDataDescriptor {
	VoxelData[CHUNK_VOXEL_COUNT] voxels;
} voxelsArray[];

readonly layout(std430, set = SCENE_DATA_SET, binding = 3) buffer voxelTypeDescriptor {
	VoxelType[] voxelTypes;
};

readonly layout(std430, set = SCENE_DATA_SET, binding = 4) buffer sceneDataDescriptor {
	vec3 localCameraPos;
	float pad0;
	ivec3 cameraChunkPos;
	int pad1;
	vec3 viewDirection;
	float pad2;
};

VoxelData GetVoxel(int chunkIndex, int x, int y, int z) {
	return voxelsArray[chunkIndex].voxels[GetVoxelIndex(x, y, z)];
}

VoxelData GetVoxel(int chunkIndex, ivec3 pos) {
	return voxelsArray[chunkIndex].voxels[GetVoxelIndex(pos)];
}

//#define VOXEL_COLOR_MATERIAL_BINDING 0
//struct VoxelColorMaterial {
//	int color;
//};
//
//readonly layout(std430, set = VOXEL_MATERIAL_SET, binding = VOXEL_COLOR_MATERIAL_BINDING) buffer voxelColorMaterialDescriptor {
//	VoxelColorMaterial[] voxelColorMaterialData;
//};

// 10 bit value, 2 interleaving bits
int PrepareMortonValue(int value) {
	value &= 0x3FF;

	value = (value | value << 16) & 0x30000FF;
	value = (value | value << 8) & 0x300F00F;
	value = (value | value << 4) & 0x30C30C3;
	value = (value | value << 2) & 0x9249249;

	return value;
}

int Morton(int x, int y, int z) {
	return PrepareMortonValue(x) | PrepareMortonValue(y) << 1 | PrepareMortonValue(z) << 2;
}

int Morton(ivec3 pos) {
	return PrepareMortonValue(pos.x) | PrepareMortonValue(pos.y) << 1 | PrepareMortonValue(pos.z) << 2;
}

VoxelData GetVoxel(ivec3 cell) {
	ivec3 chunkPos = cell >> CHUNK_SIZE_LOG2;
	int chunkIndex = chunkIndices[Morton(chunkPos)];
//	ChunkData chunk = chunkDataArray[chunkIndex];

	return GetVoxel(chunkIndex, cell & (CHUNK_SIZE - 1));
}

struct RayResult
{
	float dist;
	ivec3 cell;
	VoxelData voxel;
	bool hit;
	vec3 color;
};

float sdSphere(vec3 p, float d) { return length(p) - d; } 

float sdBox( vec3 p, vec3 b )
{
  vec3 d = abs(p) - b;
  return min(max(d.x,max(d.y,d.z)),0.0) +
         length(max(d,0.0));
}
	
bool test(ivec3 c) {
	vec3 p = vec3(c) + vec3(0.5);
	float d = min(max(-sdSphere(p, 7.5), sdBox(p, vec3(6.0))), -sdSphere(p, 25.0));
	return d < 0.0;
}

#define CHUNK_DRAW_DISTANCE 16
#define MAX_RAY_STEPS CHUNK_DRAW_DISTANCE * CHUNK_SIZE * CHUNK_SIZE
#define MAX_DIST 1000.0
RayResult VoxelRay(vec3 rayPos, vec3 rayDir)
{
	RayResult res;

	vec3 deltaDist = 1.0 / abs(rayDir);
	ivec3 rayStep = ivec3(sign(rayDir));
	res.dist = MAX_DIST;
	res.cell = ivec3(floor(rayPos));
	res.hit = false;

    bvec3 mask;
	vec3 sideDist = (sign(rayDir) * (vec3(res.cell) - rayPos) + (sign(rayDir) * 0.5) + 0.5) * deltaDist;
	for (int i = min(0, int(res.hit)); i < MAX_RAY_STEPS; i++)
	{
//		res.voxel = test(res.cell);
		if (test(res.cell)) {
//			res.dist = length(vec3(mask) * (sideDist - deltaDist));
			res.hit = true;

			res.color = vec3(0);

			if (mask.x) {
				res.color = vec3(0.5);
			}
			if (mask.y) {
				res.color = vec3(1.0);
			}
			if (mask.z) {
				res.color = vec3(0.75);
			}

			return res;
		}

		mask = lessThanEqual(sideDist.xyz, min(sideDist.yzx, sideDist.zxy));
		sideDist += vec3(mask) * deltaDist;
		res.cell += ivec3(vec3(mask)) * rayStep;
	}

	return res;
}

#include "Default/functions.glsl"

void main() {
	outColor = vec4(0.55, 0.55, 0.77, 1.0);

	vec3 rayStart = vec3(0, 10, -5);
	
	vec2 uv = (inUV - 0.5) * 2;
	uv.y = -uv.y;

	vec3 cameraDir = vec3(0.0, -1.0, 0.8);
//	cameraDir = cameraDir / length(cameraDir);
	vec3 cameraPlaneU = vec3(1.0, 0.0, 0.0);
	vec3 cameraPlaneV = vec3(0.0, 1.0, 0.0) / (1280.0/720.0);
	vec3 rayDir = cameraDir + uv.x * cameraPlaneU + uv.y * cameraPlaneV;

	RayResult result = VoxelRay(rayStart, rayDir);

	if (!result.hit) return;

	VoxelType voxelType = voxelTypes[int(result.voxel.voxelTypeIndex)];
	switch (int(result.voxel.voxelMaterialType)) {
		case -1: break;
		case 0: {
//			outColor.xyz = intToRGBA(result.voxel.voxelMaterialIndex).xyz;
			outColor.xyz = result.color;
			break;
		}
	}

	outColor.a = 1.0;
}