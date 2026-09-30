# Godot 씬을 폰·태블릿 해상도별로 띄워 PNG로 저장한다. dev:ui "Godot 모바일" 절의 화면 확인용.
# 사용: godot --path <프로젝트> --script <이 파일 절대경로> -- [scene=res://ui/main.tscn] [out=shots] [sizes=1080x2400,720x1600,1536x2048] [safe=140,100]
# 프로젝트의 기준 해상도(display/window/size)를 stretch canvas_items + aspect expand로 늘린 모습을 흉내 낸다.
# safe=위,아래(기기 px): 노치·홈 바 영역. 반투명 빨간 띠로 그리고, Engine 메타 "safe_area_preview"로 게임에 알려 준다
#   (godot-mobile.md의 SafeArea가 이 값을 읽어 여백을 준다). 띠 아래에 버튼·글자가 있으면 가려지는 것이다. safe=0,0이면 끈다.
extends SceneTree


func _initialize() -> void:
	var args := {}
	for a in OS.get_cmdline_user_args():
		var kv := a.split("=", true, 1)
		if kv.size() == 2:
			args[kv[0]] = kv[1]
	_run(args)


func _run(args: Dictionary) -> void:
	var scene_path: String = args.get("scene", ProjectSettings.get_setting("application/run/main_scene", ""))
	if scene_path == "":
		push_error("scene=res://... 를 주거나 프로젝트에 메인 씬을 지정하세요")
		quit(1)
		return
	var out: String = args.get("out", "shots")
	if out.is_relative_path() and not out.begins_with("res://") and not out.begins_with("user://"):
		out = ProjectSettings.globalize_path("res://").path_join(out)
	out = ProjectSettings.globalize_path(out)
	DirAccess.make_dir_recursive_absolute(out)
	var safe := String(args.get("safe", "140,100")).split(",")
	var base := Vector2(
		ProjectSettings.get_setting("display/window/size/viewport_width", 1152),
		ProjectSettings.get_setting("display/window/size/viewport_height", 648))

	_load_autoloads()
	var packed: PackedScene = load(scene_path)
	for s in String(args.get("sizes", "1080x2400,720x1600,1536x2048")).split(","):
		var wh := s.split("x")
		var device := Vector2i(int(wh[0]), int(wh[1]))
		var scale := minf(device.x / base.x, device.y / base.y)  # aspect expand: 짧은 쪽에 맞추고 남는 쪽은 넓힌다
		var vp := SubViewport.new()
		vp.size = device
		vp.size_2d_override = Vector2i(Vector2(device) / scale)
		vp.size_2d_override_stretch = true
		vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		vp.transparent_bg = false
		root.add_child(vp)
		var top := float(safe[0])
		var bottom := float(safe[1]) if safe.size() > 1 else 0.0
		Engine.set_meta("safe_area_preview", {"screen": Vector2(device), "top": top, "bottom": bottom})
		vp.add_child(packed.instantiate())
		_add_safe_bands(vp, top / scale, bottom / scale)
		for i in 8:
			await process_frame
		await RenderingServer.frame_post_draw
		var path := out.path_join("%s_%s.png" % [scene_path.get_file().get_basename(), s])
		vp.get_texture().get_image().save_png(path)
		print("saved ", path, "  (logical ", vp.size_2d_override, ", scale ", snappedf(scale, 0.01), ")")
		vp.queue_free()
		await process_frame
	quit()


# --script로 실행하면 autoload가 안 올라오므로, 씬이 의존할 수 있게 직접 붙인다.
func _load_autoloads() -> void:
	for p in ProjectSettings.get_property_list():
		var key: String = p.name
		if not key.begins_with("autoload/"):
			continue
		var path := String(ProjectSettings.get_setting(key)).trim_prefix("*")
		var res = load(path)
		var node: Node = res.instantiate() if res is PackedScene else res.new()
		node.name = key.trim_prefix("autoload/")
		root.add_child(node)


func _add_safe_bands(vp: SubViewport, top: float, bottom: float) -> void:
	var layer := CanvasLayer.new()
	layer.layer = 128
	vp.add_child(layer)
	for band in [[0.0, top], [1.0, bottom]]:
		if band[1] <= 0.0:
			continue
		var r := ColorRect.new()
		r.color = Color(1, 0, 0, 0.35)
		r.mouse_filter = Control.MOUSE_FILTER_IGNORE
		r.anchor_left = 0.0
		r.anchor_right = 1.0
		r.anchor_top = band[0]
		r.anchor_bottom = band[0]
		r.offset_top = 0.0 if band[0] == 0.0 else -band[1]
		r.offset_bottom = band[1] if band[0] == 0.0 else 0.0
		layer.add_child(r)
