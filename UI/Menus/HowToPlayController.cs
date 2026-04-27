using Godot;

namespace Sequence.UI.Menus;

public partial class HowToPlayController : Control
{
	[Export] public NodePath BackButtonPath { get; set; } = "CenterContainer/VBoxContainer/BackButton";

	public override void _Ready()
	{
		var backButton = GetNode<Button>(BackButtonPath);
		backButton.Pressed += OnBackPressed;
	}

	private void OnBackPressed()
	{
		GetTree().ChangeSceneToFile("res://UI/Menus/MainMenu.tscn");
	}
}
