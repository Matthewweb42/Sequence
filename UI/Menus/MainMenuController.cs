using Godot;

namespace Sequence.UI.Menus;

public partial class MainMenuController : Control
{
	[Export] public NodePath StartButtonPath { get; set; } = "CenterContainer/VBoxContainer/StartButton";
	[Export] public NodePath HowToPlayButtonPath { get; set; } = "CenterContainer/VBoxContainer/HowToPlayButton";
	[Export] public NodePath QuitButtonPath { get; set; } = "CenterContainer/VBoxContainer/QuitButton";

	public override void _Ready()
	{
		var startButton = GetNode<Button>(StartButtonPath);
		var howToPlayButton = GetNode<Button>(HowToPlayButtonPath);
		var quitButton = GetNode<Button>(QuitButtonPath);

		startButton.Pressed += OnStartPressed;
		howToPlayButton.Pressed += OnHowToPlayPressed;
		quitButton.Pressed += OnQuitPressed;
	}

	private void OnStartPressed()
	{
		GetTree().ChangeSceneToFile("res://World/World.tscn");
	}

	private void OnHowToPlayPressed()
	{
		GetTree().ChangeSceneToFile("res://UI/Menus/HowToPlay.tscn");
	}

	private void OnQuitPressed()
	{
		GetTree().Quit();
	}
}
