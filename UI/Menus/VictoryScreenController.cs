using Godot;
using Sequence.Autoloads;

namespace Sequence.UI.Menus;

public partial class VictoryScreenController : CanvasLayer
{
	[Export] public NodePath FadeRectPath { get; set; } = new("FadeRect");
	[Export] public NodePath ContentPath { get; set; } = new("Content");
	[Export] public NodePath MainMenuButtonPath { get; set; } = new("Content/CenterContainer/VBoxContainer/MainMenuButton");
	[Export] public NodePath QuitButtonPath { get; set; } = new("Content/CenterContainer/VBoxContainer/QuitButton");

	[Export] public float FadeDurationSeconds { get; set; } = 1.5f;
	[Export] public float ContentFadeDurationSeconds { get; set; } = 0.5f;

	private ColorRect? _fadeRect;
	private Control? _content;
	private Button? _mainMenuButton;
	private Button? _quitButton;
	private bool _isRunManagerSubscribed;
	private bool _hasPlayed;

	public override void _Ready()
	{
		_fadeRect = GetNodeOrNull<ColorRect>(FadeRectPath);
		_content = GetNodeOrNull<Control>(ContentPath);
		_mainMenuButton = GetNodeOrNull<Button>(MainMenuButtonPath);
		_quitButton = GetNodeOrNull<Button>(QuitButtonPath);

		if (_fadeRect != null)
		{
			_fadeRect.Color = new Color(0, 0, 0, 0);
			_fadeRect.MouseFilter = Control.MouseFilterEnum.Ignore;
		}

		if (_content != null)
		{
			_content.Visible = false;
			_content.Modulate = new Color(1, 1, 1, 0);
		}

		if (_mainMenuButton != null)
		{
			_mainMenuButton.Pressed += OnMainMenuPressed;
		}

		if (_quitButton != null)
		{
			_quitButton.Pressed += OnQuitPressed;
		}

		SubscribeRunManager();
	}

	public override void _Process(double delta)
	{
		if (!_isRunManagerSubscribed)
		{
			SubscribeRunManager();
		}
	}

	public override void _ExitTree()
	{
		UnsubscribeRunManager();

		if (_mainMenuButton != null)
		{
			_mainMenuButton.Pressed -= OnMainMenuPressed;
		}

		if (_quitButton != null)
		{
			_quitButton.Pressed -= OnQuitPressed;
		}
	}

	private void SubscribeRunManager()
	{
		if (_isRunManagerSubscribed || RunManager.Instance == null)
		{
			return;
		}

		RunManager.Instance.RunEnded += OnRunEnded;
		_isRunManagerSubscribed = true;
	}

	private void UnsubscribeRunManager()
	{
		if (!_isRunManagerSubscribed || RunManager.Instance == null)
		{
			return;
		}

		RunManager.Instance.RunEnded -= OnRunEnded;
		_isRunManagerSubscribed = false;
	}

	private void OnRunEnded(bool isVictory)
	{
		if (!isVictory || _hasPlayed)
		{
			return;
		}

		_hasPlayed = true;
		PlayVictorySequence();
	}

	private void PlayVictorySequence()
	{
		var tween = CreateTween();

		if (_fadeRect != null)
		{
			tween.TweenProperty(_fadeRect, "color:a", 1.0f, FadeDurationSeconds)
				.SetTrans(Tween.TransitionType.Sine)
				.SetEase(Tween.EaseType.InOut);
		}

		tween.TweenCallback(Callable.From(ShowContent));

		if (_content != null)
		{
			tween.TweenProperty(_content, "modulate:a", 1.0f, ContentFadeDurationSeconds);
		}
	}

	private void ShowContent()
	{
		if (_fadeRect != null)
		{
			_fadeRect.MouseFilter = Control.MouseFilterEnum.Stop;
		}

		if (_content != null)
		{
			_content.Visible = true;
			_content.MouseFilter = Control.MouseFilterEnum.Stop;
		}
	}

	private void OnMainMenuPressed()
	{
		GetTree().ChangeSceneToFile("res://UI/Menus/MainMenu.tscn");
	}

	private void OnQuitPressed()
	{
		GetTree().Quit();
	}
}
