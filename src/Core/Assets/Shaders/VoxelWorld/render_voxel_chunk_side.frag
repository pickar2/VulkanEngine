#version 450

#extension GL_EXT_nonuniform_qualifier : enable
#extension GL_AMD_gpu_shader_int16 : enable 

#include "Default/constants.glsl"
#include "Default/structs.glsl"

layout (location = 0) in vec3 inPos;

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

#define CHUNK_SIZE 8
#define CHUNK_SIZE_LOG2 3
#define CHUNK_VOXEL_COUNT CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE

#define VOXEL_MASK_BITCOUNT 4
#define INT_BITCOUNT 32
#define MASK_COMPRESSION_LEVEL (INT_BITCOUNT / VOXEL_MASK_BITCOUNT)
//#define MASK_COMPRESSION_LEVEL_LOG2 3

// 4 + 3 * 4 + CHUNK_VOXEL_COUNT / MASK_COMPRESSION_LEVEL * 4 + CHUNK_VOXEL_COUNT * 8 = 34832 bytes
struct ChunkData {
	int flags;// size: 4, offset: 0
	ivec3 chunkPos;// size: 12, offset: 4
//	uint[CHUNK_VOXEL_COUNT / MASK_COMPRESSION_LEVEL] mask;// size: 2048, offset: 16
//	VoxelData[CHUNK_VOXEL_COUNT] voxels;// size: 32768, offset: 2064
};

int GetVoxelIndex(int x, int y, int z) {
	return (z << (CHUNK_SIZE_LOG2 * 2)) | (y << CHUNK_SIZE_LOG2) | x;
}

int GetVoxelIndex(ivec3 pos) {
	return (pos.z << (CHUNK_SIZE_LOG2 * 2)) | (pos.y << CHUNK_SIZE_LOG2) | pos.x;
}

int GetChunkVoxelOffset(int chunkIndex) {
	return chunkIndex * CHUNK_VOXEL_COUNT;
}

int GetChunkMaskOffset(int chunkIndex) {
	return chunkIndex * (CHUNK_VOXEL_COUNT / MASK_COMPRESSION_LEVEL);
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
	int[] chunkIndices;// max array size = (chunk_render_distance)^3
};

readonly layout(std430, set = SCENE_DATA_SET, binding = 1) buffer chunkDataDescriptor {
	ChunkData[] chunkDataArray;
};

readonly layout(std430, set = SCENE_DATA_SET, binding = 2) buffer chunkVoxelDataDescriptor {
	VoxelData[] voxels;
};

readonly layout(std430, set = SCENE_DATA_SET, binding = 3) buffer chunkMaskDescriptor {
	uint[] mask;
};

readonly layout(std430, set = SCENE_DATA_SET, binding = 4) buffer voxelTypeDescriptor {
	VoxelType[] voxelTypes;
};

readonly layout(std430, set = SCENE_DATA_SET, binding = 5) buffer sceneDataDescriptor {
	vec3 localCameraPos;
	float pad0;
	ivec3 cameraChunkPos;
	int frameIndex;
	vec3 viewDirection;
	float pad2;
	mat4 viewMatrix;
	mat4 projMatrix;
};

VoxelData GetVoxel(int chunkIndex, int x, int y, int z) {
	return voxels[GetChunkVoxelOffset(chunkIndex) + GetVoxelIndex(x, y, z)];
}

VoxelData GetVoxel(int chunkIndex, ivec3 pos) {
	return voxels[GetChunkVoxelOffset(chunkIndex) + GetVoxelIndex(pos)];
}

VoxelData GetVoxel(int chunkIndex, int voxelIndex) {
	return voxels[GetChunkVoxelOffset(chunkIndex) + voxelIndex];
}

uint GetVoxelMask(int chunkIndex, int voxelIndex) {
	int maskIndex = voxelIndex / MASK_COMPRESSION_LEVEL;
	int bitIndex = (voxelIndex & (MASK_COMPRESSION_LEVEL - 1)) * VOXEL_MASK_BITCOUNT;
	return (mask[GetChunkMaskOffset(chunkIndex) + maskIndex] >> bitIndex) & 0xFu;
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

//int Morton(int x, int y, int z) {
//	return PrepareMortonValue(x) | PrepareMortonValue(y) << 1 | PrepareMortonValue(z) << 2;
//}

int Morton(ivec3 pos) {
	if (pos.x < 0 || pos.y < 0 || pos.z < 0) return 0;
	return PrepareMortonValue(pos.x) | PrepareMortonValue(pos.y) << 1 | PrepareMortonValue(pos.z) << 2;
}

VoxelData GetVoxel(ivec3 cell) {
	ivec3 chunkPos = cell >> CHUNK_SIZE_LOG2;
	int chunkIndex = chunkIndices[Morton(chunkPos)];

	return GetVoxel(chunkIndex, cell & (CHUNK_SIZE - 1));
}

struct RayResult
{
	float dist;
	ivec3 cell;
	VoxelData voxel;
	bool hit;
	bvec3 mask;
	vec3 sideDist;
};

float sdBox(vec3 p, vec3 b)
{
	vec3 d = abs(p) - b;
	return min(max(d.x, max(d.y, d.z)), 0.0) + length(max(d, 0.0));
}

#define CHUNK_DRAW_DISTANCE 2
#define MAX_RAY_STEPS (CHUNK_DRAW_DISTANCE * CHUNK_SIZE * 3)
#define MAX_DIST (MAX_RAY_STEPS / 1.4422)
RayResult RayMarchVoxelWorld(vec3 rayPos, vec3 rayDir)
{
	RayResult res;

	vec3 deltaDist = 1.0 / abs(rayDir);
	ivec3 rayStep = ivec3(sign(rayDir));
	res.dist = MAX_DIST;
	res.cell = ivec3(floor(rayPos));
	res.hit = false;

	vec3 sideDist = (sign(rayDir) * (vec3(res.cell) - rayPos) + (sign(rayDir) * 0.5) + 0.5) * deltaDist;
	int i = 0;

	res.mask = lessThanEqual(sideDist.xyz, min(sideDist.yzx, sideDist.zxy));
	sideDist += vec3(res.mask) * deltaDist;
	res.cell += ivec3(vec3(res.mask)) * rayStep;

	while (i < MAX_RAY_STEPS)
	{
		ivec3 chunkPos = res.cell >> CHUNK_SIZE_LOG2;
		if (chunkPos.x < 0 || chunkPos.y < 0 || chunkPos.z < 0) {
			res.mask = lessThanEqual(sideDist.xyz, min(sideDist.yzx, sideDist.zxy));
			sideDist += vec3(res.mask) * deltaDist;
			res.cell += ivec3(vec3(res.mask)) * rayStep;
			i++;

			continue;
		}

		int chunkIndex = chunkIndices[Morton(chunkPos)];
		int voxelIndex = GetVoxelIndex(res.cell & (CHUNK_SIZE - 1));
		uint voxelMask = GetVoxelMask(0, voxelIndex);
		VoxelData voxel = GetVoxel(chunkIndex, voxelIndex);

		//		if (voxelMask > 0) {
		//			for (int j = 0; j < voxelMask; j++) {
		//				res.mask = lessThanEqual(sideDist.xyz, min(sideDist.yzx, sideDist.zxy));
		//				sideDist += vec3(res.mask) * deltaDist;
		//				res.cell += ivec3(vec3(res.mask)) * rayStep;
		//				i++;
		//			}
		//		}

		if (voxelMask == 0) {
			res.voxel = GetVoxel(chunkIndex, voxelIndex);
			res.dist = length(vec3(res.mask) * (sideDist - deltaDist));
			res.hit = true;
			res.sideDist = sideDist;

			return res;
		}

		res.mask = lessThanEqual(sideDist.xyz, min(sideDist.yzx, sideDist.zxy));
		sideDist += vec3(res.mask) * deltaDist;
		res.cell += ivec3(vec3(res.mask)) * rayStep;
		i++;
	}

	return res;
}

RayResult RayMarchVoxelChunk(vec3 rayPos, vec3 rayDir)
{
	RayResult res;

	vec3 deltaDist = 1.0 / abs(rayDir);
	ivec3 rayStep = ivec3(sign(rayDir));
	res.dist = MAX_DIST;
	res.cell = ivec3(floor(rayPos));
	res.hit = false;

	vec3 sideDist = (sign(rayDir) * (vec3(res.cell) - rayPos) + (sign(rayDir) * 0.5) + 0.5) * deltaDist;
	int i = 0;

	res.mask = lessThanEqual(sideDist.xyz, min(sideDist.yzx, sideDist.zxy));
	sideDist += vec3(res.mask) * deltaDist;
	res.cell += ivec3(vec3(res.mask)) * rayStep;

	ivec3 startChunk = res.cell >> CHUNK_SIZE_LOG2;
	if (startChunk.x < 0 || startChunk.y < 0 || startChunk.z < 0) return res;

	while (i < MAX_RAY_STEPS)
	{
		ivec3 chunkPos = res.cell >> CHUNK_SIZE_LOG2;
		//		if (chunkPos.x != startChunk.x || chunkPos.y != startChunk.y || chunkPos.z != startChunk.z) {
		//			break;
		//		}

		int chunkIndex = chunkIndices[Morton(ivec3(0, 0, 0))];
		int voxelIndex = GetVoxelIndex(res.cell & (CHUNK_SIZE - 1));
		uint voxelMask = GetVoxelMask(0, voxelIndex);

		//		if (voxelMask > 0) {
		//			for (int j = 0; j < voxelMask; j++) {
		//				res.mask = lessThanEqual(sideDist.xyz, min(sideDist.yzx, sideDist.zxy));
		//				sideDist += vec3(res.mask) * deltaDist;
		//				res.cell += ivec3(vec3(res.mask)) * rayStep;
		//				i++;
		//			}
		//
		//			continue;
		//		}

		if (voxelMask == 0) {
			res.voxel = GetVoxel(chunkIndex, voxelIndex);
			res.dist = length(vec3(res.mask) * (sideDist - deltaDist));
			res.hit = true;
			res.sideDist = sideDist;

			return res;
		}

		res.mask = lessThanEqual(sideDist.xyz, min(sideDist.yzx, sideDist.zxy));
		sideDist += vec3(res.mask) * deltaDist;
		res.cell += ivec3(vec3(res.mask)) * rayStep;
		i++;
	}

	return res;
}

#include "Default/functions.glsl"

float Checker(vec3 p) {
	return step(0.0, sin(PI * p.x + PI/2.0)*sin(PI *p.y + PI/2.0)*sin(PI *p.z + PI/2.0));
}

void main() {
	outColor = vec4(0.44, 0.44, 0.88, 1.0);

	vec3 cameraPos = localCameraPos + cameraChunkPos * CHUNK_SIZE;

	vec3 rayPos = inPos;
	vec3 rayDir = rayPos - cameraPos;
	vec3 rayDirNorm = normalize(rayDir);
	rayPos -= 0.001 * rayDirNorm;

	RayResult result = RayMarchVoxelChunk(rayPos, rayDirNorm);

	if (!result.hit) discard;

	float dist = 1 - result.dist / MAX_DIST;
	dist *= dist;

	vec3 hitPos = rayPos + rayDirNorm * result.dist;
	vec3 sideUv = fract(hitPos);

	float spread = 0.05;
	outColor.xyz = vec3(result.mask) * 0.5 + 0.4;// draw normals
	//	outColor.xyz = mix(vec3(0), vec3(not(result.mask)), smoothstep(-spread, 1 + spread, sideUv + spread * 2));

	//	if (result.mask.x) {
	//		outColor.xyz = vec3(0.5);
	//	}
	//	if (result.mask.y) {
	//		outColor.xyz = vec3(1.0);
	//	}
	//	if (result.mask.z) {
	//		outColor.xyz = vec3(0.75);
	//	}
	outColor.xyz *= vec3(0.5 + 0.5 * Checker(rayPos + normalize(rayDir) * result.dist));

	//	VoxelType voxelType = voxelTypes[int(result.voxel.voxelTypeIndex)];
	//	switch (int(result.voxel.voxelMaterialType)) {
	//		case -1: break;
	//		case 0: {
	//			//			outColor.xyz = intToRGBA(result.voxel.voxelMaterialIndex).xyz;
	//			break;
	//		}
	//		case 1: {
	//			break;
	//		}
	//	}
	//	if (!result.hit) outColor.a = 0.0;

	//	outColor.a = 1.0;
}