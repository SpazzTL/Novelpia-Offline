extends Node
@onready var filter_options: Control = $FilterOptions

func _ready() -> void:
	filter_options.hide()


func _on_filter_btn_pressed() -> void:
	if filter_options.visible:
		filter_options.hide()
	else:
		filter_options.show()
