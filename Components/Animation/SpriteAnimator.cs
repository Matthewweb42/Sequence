using System;
using System.Collections.Generic;
using Godot;

namespace Sequence.Components.Animation;

/// <summary>
/// Drives a Sprite2D's RegionRect across a horizontal spritesheet to produce
/// frame-by-frame animations without needing an AnimationPlayer or AnimatedSprite2D.
/// Owned by an entity script (PlayerController, EnemyBase) and ticked from _PhysicsProcess.
/// </summary>
public sealed class SpriteAnimator
{
	private sealed class Clip
	{
		public required string Name { get; init; }
		public required Texture2D Sheet { get; init; }
		public required int FirstFrameX { get; init; }
		public required int FirstFrameY { get; init; }
		public required int FrameCount { get; init; }
		public required float DefaultFps { get; init; }
		public required bool Loop { get; init; }
	}

	private readonly Sprite2D _sprite;
	private readonly int _frameWidth;
	private readonly int _frameHeight;
	private readonly Dictionary<string, Clip> _clips = new();

	private Clip? _active;
	private float _elapsed;
	private float _frameDuration;
	private bool _finishedFired;

	public string CurrentClip => _active?.Name ?? string.Empty;
	public bool IsPlaying => _active != null;

	public event Action<string>? Finished;

	public SpriteAnimator(Sprite2D sprite, int frameWidth, int frameHeight)
	{
		_sprite = sprite;
		_frameWidth = frameWidth;
		_frameHeight = frameHeight;
	}

	public void RegisterClip(string name, Texture2D sheet, int firstFrameX, int firstFrameY, int frameCount, float defaultFps, bool loop)
	{
		_clips[name] = new Clip
		{
			Name = name,
			Sheet = sheet,
			FirstFrameX = firstFrameX,
			FirstFrameY = firstFrameY,
			FrameCount = Mathf.Max(1, frameCount),
			DefaultFps = Mathf.Max(0.01f, defaultFps),
			Loop = loop,
		};
	}

	/// <summary>
	/// Starts a clip. If totalDurationOverride is supplied the clip's frame
	/// pacing is stretched/compressed to exactly fill that duration (useful for
	/// syncing a swing animation to an attack window).
	/// </summary>
	public void Play(string name, float? totalDurationOverride = null)
	{
		if (!_clips.TryGetValue(name, out var clip))
		{
			return;
		}

		// Avoid restarting if the same clip is already mid-play.
		if (_active == clip && !_finishedFired)
		{
			return;
		}

		_active = clip;
		_elapsed = 0f;
		_finishedFired = false;
		_frameDuration = totalDurationOverride.HasValue
			? Mathf.Max(0.001f, totalDurationOverride.Value / clip.FrameCount)
			: 1f / clip.DefaultFps;

		ApplyFrame(0);
	}

	/// <summary>
	/// Stops the current clip and snaps the sprite to frame 0 of the most
	/// recently played clip (so the entity has a sensible static pose).
	/// </summary>
	public void Stop()
	{
		if (_active == null)
		{
			return;
		}

		_elapsed = 0f;
		_finishedFired = false;
		ApplyFrame(0);
		_active = null;
	}

	public void Tick(float delta)
	{
		if (_active == null)
		{
			return;
		}

		_elapsed += delta;

		var rawFrame = (int)(_elapsed / _frameDuration);
		int frameIndex;
		if (_active.Loop)
		{
			frameIndex = rawFrame % _active.FrameCount;
		}
		else
		{
			frameIndex = Mathf.Min(rawFrame, _active.FrameCount - 1);
			if (rawFrame >= _active.FrameCount && !_finishedFired)
			{
				_finishedFired = true;
				Finished?.Invoke(_active.Name);
			}
		}

		ApplyFrame(frameIndex);
	}

	private void ApplyFrame(int frameIndex)
	{
		if (_active == null)
		{
			return;
		}

		_sprite.Texture = _active.Sheet;
		_sprite.RegionEnabled = true;
		_sprite.RegionRect = new Rect2(
			_active.FirstFrameX + frameIndex * _frameWidth,
			_active.FirstFrameY,
			_frameWidth,
			_frameHeight);
	}
}
