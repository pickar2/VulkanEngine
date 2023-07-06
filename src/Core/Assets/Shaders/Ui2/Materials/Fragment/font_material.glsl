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

const float sdfBorderSize = 0.009766;
const vec2 sdfTexel = 1.0 / vec2(1024.0,1024.0);

const float subpixel_amount = 1.0;
const float hint_amount = 1.0;

vec3 sdf_triplet_alpha(float dOffset, vec3 sdf, float horz_scale, float vert_scale, float vgrad) {
	float hdoffset = mix(dOffset * horz_scale, dOffset * vert_scale, vgrad);
	float rdoffset = mix(dOffset, hdoffset, hint_amount);
	vec3 alpha = smoothstep(vec3(0.5 - rdoffset), vec3(0.5 + rdoffset), sdf);
	alpha = pow(alpha, vec3(1.0 + 0.2 * vgrad * hint_amount));
	return alpha;
}

float sdf_alpha(float dOffset, float sdf, float horz_scale, float vert_scale, float vgrad) {
	float hdoffset = mix(dOffset * horz_scale, dOffset * vert_scale, vgrad);
	float rdoffset = mix(dOffset, hdoffset, hint_amount);
	float alpha = smoothstep(0.5 - rdoffset, 0.5 + rdoffset, sdf);
	alpha = pow(alpha, 1.0 + 0.2 * vgrad * hint_amount);
	return alpha;
}

vec3 subpixel( float v, float a ) {
    float vt      = 0.6 * v; // 1.0 will make your eyes bleed
    vec3  rgb_max = vec3( -vt, 0.0, vt );
    float top     = abs( vt );
    float bottom  = -top - 1.0;
    float cfloor  = mix( top, bottom, a );
    vec3  res     = clamp( rgb_max - vec3( cfloor ), 0.0, 1.0 );
    return res;
}

void font_material(UiElementData data) {
	font_material_struct mat = font_material_data[data.fragmentDataIndex];

	float scale = 1 / 32.0;
	float sdfSize = 2.0 * scale * sdfBorderSize;
	float subpixelOffset = 0.3333 / scale;
	float dOffset = 1.0 / sdfSize;

	vec4 color = vec4(0, 0, 0, 1);//intToRGBA(mat.color);
	
	
	float sdf       = texture( textures[mat.textureId], fragTexCoord ).a;
    float sdf_north = texture( textures[mat.textureId], fragTexCoord + vec2( 0.0, sdfTexel.y ) ).a;
    float sdf_east  = texture( textures[mat.textureId], fragTexCoord + vec2( sdfTexel.x, 0.0 ) ).a;

    // Estimating stroke direction by the distance field gradient vector
    vec2  sgrad     = vec2( sdf_east - sdf, sdf_north - sdf );
    float sgrad_len = max( length( sgrad ), 1.0 / 128.0 );
    vec2  grad      = sgrad / vec2( sgrad_len );
    float vgrad = abs( grad.y ); // 0.0 - vertical stroke, 1.0 - horizontal one
    
    float horz_scale  = 1.1; // Blurring vertical strokes along the X axis a bit
    float vert_scale  = 0.6; // While adding some contrast to the horizontal strokes
    float hdoffset    = mix( dOffset * horz_scale, dOffset * vert_scale, vgrad ); 
    float res_doffset = mix( dOffset, hdoffset, hint_amount );
    
    float alpha       = smoothstep( 0.5 - res_doffset, 0.5 + res_doffset, sdf );

    // Additional contrast
    alpha             = pow( alpha, 1.0 + 0.2 * vgrad * hint_amount );

    // Unfortunately there is no support for ARB_blend_func_extended in WebGL.
    // Fortunately the background is filled with a solid color so we can do
    // the blending inside the shader.
    
    // Discarding pixels beyond a threshold to minimise possible artifacts.
    
    vec3 channels = subpixel( grad.x * 0.5 * subpixel_amount, alpha );

    // For subpixel rendering we have to blend each color channel separately
    vec3 res = mix( vec3(0.7), color.rgb, channels );

    outColor = vec4( color.rgb, color.a);

	if (sdf < 0.333) outColor.a = 0;
    if ( alpha < 20.0 / 256.0 ) outColor.a = 0;
	

//	float sdf       = texture(textures[mat.textureId], fragTexCoord).a;
//	float sdf_north = texture(textures[mat.textureId], fragTexCoord + vec2(0.0, sdfTexel.y)).a;
//	float sdf_east  = texture(textures[mat.textureId], fragTexCoord + vec2(sdfTexel.x, 0.0)).a;
//	// Estimating stroke direction by the distance field gradient vector
//	vec2  sgrad     = vec2(sdf_east - sdf, sdf_north - sdf);
//	float sgrad_len = max(length(sgrad), 1.0 / 128.0);
//	vec2  grad      = sgrad / vec2(sgrad_len);
//	float vgrad = abs(grad.y);// 0.0 - vertical stroke, 1.0 - horizontal one
//	if (subpixel_amount > 0.0) {
//		// Subpixel SDF samples
//		vec2  subpixel = vec2(subpixelOffset, 0.0);
//
//		// For displays with vertical subpixel placement:
//		// vec2 subpixel = vec2( 0.0, subpixelOffset );
//
//		float sdf_sp_n  = texture(textures[mat.textureId], fragTexCoord - subpixel).a;
//		float sdf_sp_p  = texture(textures[mat.textureId], fragTexCoord + subpixel).a;
//		float horz_scale  = 0.5;// Should be 0.33333, a subpixel size, but that is too colorful
//		float vert_scale  = 0.6;
//		vec3 triplet_alpha = sdf_triplet_alpha(dOffset, vec3(sdf_sp_n, sdf, sdf_sp_p), horz_scale, vert_scale, vgrad);
//
//		// For BGR subpixels:
//		// triplet_alpha = triplet.bgr
//		outColor = vec4(color.rgb * triplet_alpha, color.a);
//	} else {
//		float horz_scale  = 1.1;
//		float vert_scale  = 0.6;
//
//		float alpha = sdf_alpha(dOffset, sdf, 1.1, 0.6, vgrad);
//		outColor = vec4(color.rgb, color.a);
//	}
//
//	if(sdf < 0.33) outColor.a = 0;

//	if (outColor.a < 0.9) outColor.a = 0;

	//	float outlineDistanceScaled = clamp(0, 0.5, 0.45 * mat.scale);
	//	
	//	float smoothing = 0.25 / (4 * mat.scale);
	//	float distance = texture(textures[mat.textureId], fragTexCoord).a;
	//	float outlineFactor = smoothstep(0.5 - smoothing, 0.5 + smoothing, distance);
	//	vec4 color = mix(outlineColor, textColor, outlineFactor);
	//	float alpha = smoothstep(outlineDistanceScaled - smoothing, outlineDistanceScaled + smoothing, distance);
	//	outColor = vec4(color.rgb, alpha);


	//	float smoothing = 0.25 / (4 * mat.scale);
	//	float alpha = smoothstep(0.5-smoothing, 0.5+smoothing, texture(textures[mat.textureId], fragTexCoord).a);
	//	float lowThreshold = 0.5;
	//	float alpha = texture(textures[mat.textureId], fragTexCoord).r;
	//
	//    if ( alpha >= lowThreshold ) {
	//        alpha   = 1.0;
	//    }
	//    else {
	//        alpha   = 0.0;
	//    }
	//	
	//	outColor = vec4(textColor.rgb, alpha);
}
