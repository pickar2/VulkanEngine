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
//	if (pos.x < 0 || pos.y < 0 || pos.z < 0) return 0;
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

float sdBox( vec3 p, vec3 b )
{
  vec3 d = abs(p) - b;
  return min(max(d.x,max(d.y,d.z)),0.0) + length(max(d,0.0));
}

#define CHUNK_DRAW_DISTANCE 64
#define MAX_RAY_STEPS (CHUNK_DRAW_DISTANCE * CHUNK_SIZE)
#define MAX_DIST (MAX_RAY_STEPS / 1.4422)
RayResult VoxelRay(vec3 rayPos, vec3 rayDir)
{
	RayResult res;

	vec3 deltaDist = 1.0 / abs(rayDir);
	ivec3 rayStep = ivec3(sign(rayDir));
	res.dist = MAX_DIST;
	res.cell = ivec3(floor(rayPos));
	res.hit = false;

	vec3 sideDist = (sign(rayDir) * (vec3(res.cell) - rayPos) + (sign(rayDir) * 0.5) + 0.5) * deltaDist;
	int i = 0;
	while (i < MAX_RAY_STEPS)
	{
		ivec3 chunkPos = res.cell >> CHUNK_SIZE_LOG2;
		if (chunkPos.x < 0 || chunkPos.y < 0 || chunkPos.z < 0) {
			res.mask = lessThanEqual(sideDist.xyz, min(sideDist.yzx, sideDist.zxy));
			sideDist += vec3(res.mask) * deltaDist;
			res.cell += ivec3(vec3(res.mask)) * rayStep;
			i+=1;

			continue;
		}

		int chunkIndex = chunkIndices[Morton(chunkPos)];
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

		if (voxelMask == 0 && i != 0) {
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

	vec2 uv = vec2(2 * inUV.x - 1, 1 - 2 * inUV.y);

	float width = 1280;
	float height = 720;
	float aspect = width/height;

	const float fov = 90;
	const float mult = tan(fov / 2 * PI / 180);
//	vec3 cameraDir = vec3(viewDirection.xy, -1);

	float Px = uv.x * mult * aspect;
	float Py = uv.y * mult;

	vec3 rayPos = localCameraPos + cameraChunkPos * CHUNK_SIZE;
//	mat4 newView = viewMatrix;
//	newView[0][0] = 2;
//	newView[1][1] = 2;
//	newView[2][2] = 2;
//	newView[3][3] = 2;
	vec3 rayDir = (viewMatrix * vec4(Px, Py, -1, 1)).xyz - rayPos;
	vec3 rayDirNorm = normalize(rayDir);

	RayResult result = VoxelRay(rayPos, rayDirNorm);

	if (!result.hit) return;

	float dist = 1 - result.dist / MAX_DIST;
	dist *= dist;

//	outColor.xyz = vec3(result.mask); // draw normals
//	outColor.xyz = uv.xxx;

	vec3 hitPos = rayPos + rayDirNorm * result.dist;
	vec3 sideUv = fract(hitPos);
	outColor.xyz = mix(vec3(0), vec3(not(result.mask)), sideUv);
//	if (result.mask.x) {
//		outColor.xyz = vec3(0.5);
//	}
//	if (result.mask.y) {
//		outColor.xyz = vec3(1.0);
//	}
//	if (result.mask.z) {
//		outColor.xyz = vec3(0.75);
//	}
//	outColor.xyz *= vec3(dist);
//    outColor.xyz *= vec3(0.5 + 0.5 * Checker(rayPos + normalize(rayDir) * result.dist));
//	outColor.a *= dist;
//	outColor.rgb = vec3(dist);

	VoxelType voxelType = voxelTypes[int(result.voxel.voxelTypeIndex)];
	switch (int(result.voxel.voxelMaterialType)) {
		case -1: break;
		case 0: {
//			outColor.xyz = intToRGBA(result.voxel.voxelMaterialIndex).xyz;
			break;
		}
		case 1: {
			break;
		}
	}

	outColor.a = 1.0;
}