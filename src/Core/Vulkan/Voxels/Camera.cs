using System;
using Core.UI;
using Core.Utils;
using Core.Window;
using SDL2;
using SimpleMath.Vectors;

namespace Core.Vulkan.Voxels;

public class Camera
{
	private const float HorizontalSpeed = 1;
	private const float VerticalSpeed = 1;
	private const float MouseSensitivity = 0.25f;

	private const double DefaultSpeedMultiplier = 5;
	private double _speedMultiplier = DefaultSpeedMultiplier / Context.MsPerUpdate;

	public Vector3<double> Position;
	public Vector3<double> Direction;

	public void MoveDirection(double yaw, double pitch, double roll)
	{
		Direction.X = (Direction.X + yaw) % 360d;
		Direction.Y = Math.Clamp(Direction.Y + pitch, -89, 89);
		Direction.Z = (Direction.Z + roll) % 360d;
	}

	public Camera()
	{
		Position = new Vector3<double>(0, 0, 0);

		MouseInput.OnMouseDragMove += (_, motion, _) => MoveDirection(-motion.X * MouseSensitivity, -motion.Y * MouseSensitivity, 0);

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
		};
	}
}
