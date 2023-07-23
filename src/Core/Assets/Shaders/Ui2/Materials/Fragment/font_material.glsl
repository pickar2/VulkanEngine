//identifier=font_material
//type=FragmentShader
//size=16

struct font_material_struct { // 16 bytes (aligned for 4 bytes)
	int textureId;
	float scale;
	int color;
	float outlineDistance;
};

readonly layout(std430, set = FRAGMENT_MATERIAL_SET, binding = font_material_binding) buffer font_material_buffer {
	font_material_struct font_material_data[];
};

const vec4 outlineColor = vec4(0, 0, 0, 1);

const float subpixel_amount = 1.0;
const float hint_amount = 1.0;

vec3 estimateSubpixelCoverage(vec2 grad, float pixelCoverage, float subpixelLevel, bool isBgr, bool isVertical) {
	float slope = isVertical ? grad.y : grad.x;
	slope *= 0.333 * subpixelLevel;// empirical value
	/* Check for inequality because if we flip twice (both because of the sign of the slope and because of the BGR pattern) we effectively do not flip. */
	bool flip = isVertical
	? (slope < 0.0) != isBgr
	: (slope > 0.0) != isBgr;
	vec3 subpixelPositions = flip ?  vec3(2, 1, 0) : vec3(0, 1, 2);

	vec3 subpixelCoverage = pixelCoverage + abs(slope) * (2.0 * pixelCoverage - subpixelPositions);
	subpixelCoverage = clamp(subpixelCoverage, 0.0, 1.0);

	return subpixelCoverage;
}

//vec3 sdf_triplet_alpha( vec3 sdf, float horz_scale, float vert_scale, float vgrad, float doffset ) {
//    float hdoffset = mix( doffset * horz_scale, doffset * vert_scale, vgrad );
//    float rdoffset = mix( doffset, hdoffset, hint_amount );
//    vec3 alpha = smoothstep( vec3( 0.5 - rdoffset ), vec3( 0.5 + rdoffset ), sdf );
//    alpha = pow( alpha, vec3( 1.0 + 0.2 * vgrad * hint_amount ) );
//    return alpha;
//}

void font_material(UiElementData data) {
	font_material_struct mat = font_material_data[data.fragmentDataIndex];

	//	float scale = mat.scale;
	vec4 color = intToRGBA(mat.color);

	vec2 sdf_texel = vec2(1.0 / 2048.0);
	float doffset = 1.0 / mat.scale;

	// Sampling the texture, L pattern
	float sdf       = 2.52 * texture(textures[mat.textureId], fragTexCoord).r;
	float sdf_north = 2.52 * texture(textures[mat.textureId], fragTexCoord + vec2(0.0, sdf_texel.y)).r;
	float sdf_east  = 2.52 * texture(textures[mat.textureId], fragTexCoord + vec2(sdf_texel.x, 0.0)).r;

	//	sdf = sdf * sdf;
	//	sdf_north = sdf_north * sdf_north;
	//	sdf_east = sdf_east * sdf_east;

	// Estimating stroke direction by the distance field gradient vector
	vec2  sgrad     = vec2(sdf_east - sdf, sdf_north - sdf);
	float sgrad_len = max(length(sgrad), 1.0 / 128.0);
	vec2  grad      = sgrad / vec2(sgrad_len);
	float vgrad = abs(grad.y);// 0.0 - vertical stroke, 1.0 - horizontal one

	float horz_scale  = 1;// Blurring vertical strokes along the X axis a bit
	float vert_scale  = 1;// While adding some contrast to the horizontal strokes
	float hdoffset    = mix(doffset * horz_scale, doffset * vert_scale, vgrad);
	float res_doffset = mix(doffset, hdoffset, hint_amount) * 2;

	float alpha       = smoothstep(0.5 - res_doffset, 0.5 + res_doffset, sdf);

	// Additional contrast
	alpha             = pow(alpha, 1.0 + 0.2 * vgrad * hint_amount);
	if (alpha < 20.0 / 256.0) alpha = 0;

	vec3 channels = estimateSubpixelCoverage(grad, alpha, subpixel_amount, false, false);

	//	if(sdf < 0.3) discard;
	outColor = vec4(channels * color.rgb, alpha);

	//    vec2  subpixel = vec2( 1920.0 / 3.0, 0.0 );
	//    
	//    // For displays with vertical subpixel placement:
	//    // vec2 subpixel = vec2( 0.0, subpixel_offset );
	//    
	//    float sdf_sp_n  = 2.5 * texture( textures[mat.textureId], fragTexCoord - subpixel ).r;
	//    float sdf_sp_p  = 2.5 * texture( textures[mat.textureId], fragTexCoord + subpixel ).r;
	//
	//    horz_scale  = 0.5; // Should be 0.33333, a subpixel size, but that is too colorful
	//    vert_scale  = 0.6;
	//
	//    vec3 channels = sdf_triplet_alpha( vec3( sdf_sp_n, sdf, sdf_sp_p ), horz_scale, vert_scale, vgrad, doffset );
	//    outColor = vec4( channels * color.xyz, alpha * color.a );
}
