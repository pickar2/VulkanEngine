using System;
using Core.UI;
using Core.UI.Controls;
using Core.Utils;
using Core.Vulkan.Renderers;
using Core.Window;
using SDL2;
using SimpleMath.Vectors;

namespace Core.Vulkan.Voxels;

public class VoxelCamera
{
	private const float HorizontalSpeed = 1;
	private const float VerticalSpeed = 1;
	private const float MouseSensitivity = 0.25f;

	private static double DefaultSpeedMultiplier => 10 / Context.MsPerUpdate;
	private double _speedMultiplier = DefaultSpeedMultiplier;

	public Vector3<double> Position;
	public Vector3<double> YawPitchRoll;
	public Vector3<int> ChunkPos;

	private readonly Label _label;

	public void MoveDirection(double yaw, double pitch, double roll)
	{
		YawPitchRoll.X = (YawPitchRoll.X + yaw) % 360d;
		YawPitchRoll.Y = Math.Clamp(YawPitchRoll.Y + pitch, -89, 89);
		YawPitchRoll.Z = (YawPitchRoll.Z + roll) % 360d;

		// App.Logger.Info.Message($"({yaw}, {pitch}, {roll}) : {YawPitchRoll}");
	}

	public Vector3<double> Direction =>
		new()
		{
			X = Math.Cos(YawPitchRoll.X.ToRadians()) * Math.Sin(YawPitchRoll.Y.ToRadians()),
			Y = Math.Sin(YawPitchRoll.X.ToRadians()) * Math.Sin(YawPitchRoll.Y.ToRadians()),
			Z = Math.Cos(YawPitchRoll.Y.ToRadians())
		};

	public void UpdatePosition()
	{
		ChunkPos.X += (int) Math.Floor(Position.X / VoxelChunk.ChunkSize);
		Position.X = (Position.X + VoxelChunk.ChunkSize) % VoxelChunk.ChunkSize;

		ChunkPos.Y += (int) Math.Floor(Position.Y / VoxelChunk.ChunkSize);
		Position.Y = (Position.Y + VoxelChunk.ChunkSize) % VoxelChunk.ChunkSize;

		ChunkPos.Z += (int) Math.Floor(Position.Z / VoxelChunk.ChunkSize);
		Position.Z = (Position.Z + VoxelChunk.ChunkSize) % VoxelChunk.ChunkSize;

		var pos = Position + ChunkPos * VoxelChunk.ChunkSize;
		_label.Text = $"({Maths.FixedPrecision(pos.X, 2)}, {Maths.FixedPrecision(pos.Y, 2)}, {Maths.FixedPrecision(pos.Z, 2)})";
	}

	public void SetPosition(double x, double y, double z)
	{
		Position = new Vector3<double>(x, y, z);
		ChunkPos = new Vector3<int>(0, 0, 0);
		UpdatePosition();
	}

	public VoxelCamera()
	{
		_label = new Label(GeneralRenderer.UiContext);
		_label.OffsetZ = 1000;
		_label.MarginLT = (265, 60);
		GeneralRenderer.MainRoot.AddChild(_label);

		Position = new Vector3<double>(8, 8, 8);
		UpdatePosition();

		UiManager.InputContext.MouseInputHandler.OnMouseDragMove +=
			(_, motion, _) => MoveDirection(motion.X * MouseSensitivity, motion.Y * MouseSensitivity, 0);

		KeyboardInputHandler.OnKeyDown += (key) =>
		{
			_speedMultiplier = key.sym switch
			{
				SDL.SDL_Keycode.SDLK_LCTRL => DefaultSpeedMultiplier * 2.7,
				SDL.SDL_Keycode.SDLK_LSHIFT => DefaultSpeedMultiplier * 0.4,
				_ => _speedMultiplier
			};
		};

		KeyboardInputHandler.OnKeyUp += (key) =>
		{
			if (key.sym is SDL.SDL_Keycode.SDLK_LCTRL or SDL.SDL_Keycode.SDLK_LSHIFT) _speedMultiplier = DefaultSpeedMultiplier;
		};

		UiManager.BeforeUpdate += () =>
		{
			var relativeMoveVector = new Vector3<int>();

			if (UiManager.InputContext.KeyboardInputHandler.IsKeyPressed(SDL.SDL_Keycode.SDLK_w)) relativeMoveVector.Z = -1;
			else if (UiManager.InputContext.KeyboardInputHandler.IsKeyPressed(SDL.SDL_Keycode.SDLK_s)) relativeMoveVector.Z = 1;

			if (UiManager.InputContext.KeyboardInputHandler.IsKeyPressed(SDL.SDL_Keycode.SDLK_a)) relativeMoveVector.X = -1;
			else if (UiManager.InputContext.KeyboardInputHandler.IsKeyPressed(SDL.SDL_Keycode.SDLK_d)) relativeMoveVector.X = 1;

			if (UiManager.InputContext.KeyboardInputHandler.IsKeyPressed(SDL.SDL_Keycode.SDLK_z)) relativeMoveVector.Y = -1;
			else if (UiManager.InputContext.KeyboardInputHandler.IsKeyPressed(SDL.SDL_Keycode.SDLK_SPACE)) relativeMoveVector.Y = 1;

			double yaw = YawPitchRoll.X.ToRadians();

			if (relativeMoveVector.Z != 0)
			{
				Position.X -= Math.Sin(yaw) * relativeMoveVector.Z * _speedMultiplier * HorizontalSpeed;
				Position.Z += Math.Cos(yaw) * relativeMoveVector.Z * _speedMultiplier * HorizontalSpeed;
			}

			if (relativeMoveVector.X != 0)
			{
				Position.X += Math.Cos(yaw) * relativeMoveVector.X * _speedMultiplier * HorizontalSpeed;
				Position.Z += Math.Sin(yaw) * relativeMoveVector.X * _speedMultiplier * HorizontalSpeed;
			}

			Position.Y += relativeMoveVector.Y * _speedMultiplier * VerticalSpeed;

			UpdatePosition();
		};
	}
}
