using Godot;

namespace Sequence.UI.Menus;

public partial class MainMenuController : Control
{
	[Export] public NodePath StartButtonPath { get; set; } = "CenterContainer/VBoxContainer/StartButton";
	[Export] public NodePath QuitButtonPath { get; set; } = "CenterContainer/VBoxContainer/QuitButton";

	public override void _Ready()
	{
		var startButton = GetNode<Button>(StartButtonPath);
		var quitButton = GetNode<Button>(QuitButtonPath);

		startButton.Pressed += OnStartPressed;
		quitButton.Pressed += OnQuitPressed;
	}

	private void OnStartPressed()
	{
		GetTree().ChangeSceneToFile("res://World/World.tscn");
	}

	private void OnQuitPressed()
	{
		GetTree().Quit();
	}
}
