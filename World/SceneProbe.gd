extends CanvasLayer

@onready var _label: Label = $MarkerPanel/MarkerLabel

func _ready() -> void:
	var root: Node = get_parent()
	var script_res: Script = root.get_script() as Script
	var script_path: String = "<null>"
	if script_res != null:
		script_path = script_res.resource_path

	var lines: PackedStringArray = PackedStringArray([
		"WORLD.TSCN LIVE (probe)",
		"Root script: %s" % script_path,
		"Has EnemyContainer: %s" % str(root.has_node("EnemyContainer")),
		"If Root script is <null>, C# script is not loading in editor runtime",
	])

	_label.text = "\n".join(lines)
	print("[SceneProbe] Root script:", script_path)
