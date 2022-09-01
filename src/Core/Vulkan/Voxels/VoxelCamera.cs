using System;
using Core.UI;
using Core.Utils;
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
	public Vector3<double> Direction;
	public Vector3<int> ChunkPos;

	public void MoveDirection(double yaw, double pitch, double roll)
	{
		Direction.X = (Direction.X + yaw) % 360d;
		Direction.Y = Math.Clamp(Direction.Y + pitch, -89, 89);
		Direction.Z = (Direction.Z + roll) % 360d;
	}

	public void UpdatePosition()
	{
		ChunkPos.X += (int) Math.Floor(Position.X / VoxelChunk.ChunkSize);
		Position.X = (Position.X + VoxelChunk.ChunkSize) % VoxelChunk.ChunkSize;

		ChunkPos.Y += (int) Math.Floor(Position.Y / VoxelChunk.ChunkSize);
		Position.Y = (Position.Y + VoxelChunk.ChunkSize) % VoxelChunk.ChunkSize;

		ChunkPos.Z += (int) Math.Floor(Position.Z / VoxelChunk.ChunkSize);
		Position.Z = (Position.Z + VoxelChunk.ChunkSize) % VoxelChunk.ChunkSize;

		// App.Logger.Info.Message($"{ChunkPos} | {Position}");
	}

	public void SetPosition(double x, double y, double z)
	{
		Position = new Vector3<double>(x, y, z);
		UpdatePosition();
	}

	public VoxelCamera()
	{
		Position = new Vector3<double>(8, 8, 8);

		MouseInput.OnMouseDragMove += (_, motion, _) => MoveDirection(-motion.X * MouseSensitivity, -motion.Y * MouseSensitivity, 0);

		KeyboardInput.OnKeyDown += (key) =>
		{
			_speedMultiplier = key.sym switch
			{
				SDL.SDL_Keycode.SDLK_LCTRL => DefaultSpeedMultiplier * 2.7,
				SDL.SDL_Keycode.SDLK_LSHIFT => DefaultSpeedMultiplier * 0.4,
				_ => _speedMultiplier
			};
		};

		KeyboardInput.OnKeyUp += (key) =>
		{
			if (key.sym is SDL.SDL_Keycode.SDLK_LCTRL or SDL.SDL_Keycode.SDLK_LSHIFT) _speedMultiplier = DefaultSpeedMultiplier;
		};

		UiManager.BeforeUpdate += () =>
		{
			var moveDirection = new Vector3<int>();

			if (KeyboardInput.IsKeyPressed(SDL.SDL_Keycode.SDLK_w)) moveDirection.Z = 1;
			else if (KeyboardInput.IsKeyPressed(SDL.SDL_Keycode.SDLK_s)) moveDirection.Z = -1;

			if (KeyboardInput.IsKeyPressed(SDL.SDL_Keycode.SDLK_a)) moveDirection.X = -1;
			else if (KeyboardInput.IsKeyPressed(SDL.SDL_Keycode.SDLK_d)) moveDirection.X = 1;

			if (KeyboardInput.IsKeyPressed(SDL.SDL_Keycode.SDLK_z)) moveDirection.Y = -1;
			else if (KeyboardInput.IsKeyPressed(SDL.SDL_Keycode.SDLK_SPACE)) moveDirection.Y = 1;

			double yaw = Math.PI - Direction.X.ToRadians();

			if (moveDirection.Z != 0)
			{
				Position.X -= Math.Sin(yaw) * moveDirection.Z * _speedMultiplier * HorizontalSpeed;
				Position.Z += Math.Cos(yaw) * moveDirection.Z * _speedMultiplier * HorizontalSpeed;
			}

			if (moveDirection.X != 0)
			{
				Position.X -= Math.Cos(yaw) * moveDirection.X * _speedMultiplier * HorizontalSpeed;
				Position.Z -= Math.Sin(yaw) * moveDirection.X * _speedMultiplier * HorizontalSpeed;
			}

			Position.Y += moveDirection.Y * _speedMultiplier * VerticalSpeed;

			UpdatePosition();
		};
	}
}
