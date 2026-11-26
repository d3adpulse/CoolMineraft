shader_type spatial;
render_mode cull_back, diffuse_burley;

// Атлас 4x4 по 16x16 пикселей. UV приходят как локальные координаты внутри тайла,
// поэтому greedy-квады могут быть шире одного блока: fract() повторяет тайл.
uniform sampler2D atlas : source_color, filter_nearest, repeat_disable;
uniform vec2 grid = vec2(4.0, 4.0);
// Полтекселя в долях тайла: 0.5 / 16 = 0.03125. Прижимаемся к центрам пикселей,
// чтобы Nearest не захватывал соседний тайл.
uniform float inset = 0.03125;

varying vec2 tile_index;

void vertex() {
	tile_index = UV2;
}

void fragment() {
	vec2 tile = vec2(floor(tile_index.x + 0.5), floor(tile_index.y + 0.5));
	vec2 size = 1.0 / grid;
	vec2 origin = tile * size;

	vec2 local = fract(UV);
	local = clamp(local, vec2(inset), vec2(1.0 - inset));

	vec4 tex = texture(atlas, origin + local * size);
	ALBEDO = tex.rgb * COLOR.rgb;
	ALPHA = 1.0;
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
}
