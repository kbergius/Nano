using System;

namespace Nano
{
	/// <summary>
	/// Specifies the frame pacing strategy of the Game's tick loop.
	/// </summary>
	public enum FramePacingMode
	{
		/// <summary>
		/// The game will render at the same pace as the timestep.
		/// The tick loop will wait on the swapchain right before events are processed to minimize visual latency.
		/// Note that this will lead to lower throughput in GPU-bound scenarios.
		/// </summary>
		LatencyOptimized,
		/// <summary>
		/// The game will render no more than the specified frames per second.
		/// The framerate limit may be different from the timestep.
		/// </summary>
		Capped,
		/// <summary>
		/// The game will render at the maximum possible framerate that the computing resources allow. <br/>
		/// Note that this may lead to overheating, resource starvation, etc. <br/>
		/// The framerate limit may be different from the timestep.
		/// If the GraphicsDevice.PresentMode is set to VSYNC, the framerate will be limited by the monitor refresh rate.
		/// </summary>
		Uncapped
	}

	/// <summary>
	/// The Game's frame pacing settings. Specifies uncapped framerate or a maximum rendering frames per second value. <br/>
	/// </summary>
	public struct FramePacingSettings
	{
		/// <summary>
		/// Specifies the frame pacing strategy of the Game's tick loop.
		/// </summary>
		public FramePacingMode Mode { get; private set;}

		/// <summary>
		/// Represents how often Game.Update will called.
		/// </summary>
		public TimeSpan Timestep { get; private set; }

		/// <summary>
 		/// If Mode is set to Uncapped, this will be ignored and Game.Draw will run as fast as possible.
		/// If Mode is set to Capped, this represents how often Game.Draw will be called.
		/// If Mode is set to LatencyOptimized, this value will be ignored and Game.Draw will run at the same rate as Game.Update.
		/// </summary>
		public TimeSpan FramerateCapTimestep { get; private set; }

		/// <summary>
		/// The game will render at the same pace as the timestep.
		/// The tick loop will wait on the swapchain right before events are processed to minimize visual latency.
		/// Note that this will lead to lower throughput in GPU-bound scenarios.
		/// </summary>
		public static FramePacingSettings CreateLatencyOptimized(
			int timestepFPS
		) {
			return new FramePacingSettings(
				FramePacingMode.LatencyOptimized,
				timestepFPS,
				timestepFPS
			);
		}

		/// <summary>
		/// The game will render no more than the specified frames per second.
		/// The framerate limit may be different from the timestep.
		/// </summary>
		public static FramePacingSettings CreateCapped(
			int timestepFPS,
			int framerateCapFPS
		) {
			return new FramePacingSettings(
				FramePacingMode.Capped,
				timestepFPS,
				framerateCapFPS
			);
		}

		/// <summary>
		/// The game will render at the maximum possible framerate that the computing resources allow. <br/>
		/// Note that this may lead to overheating, resource starvation, etc. <br/>
		/// The framerate limit may be different from the timestep.
		/// If the GraphicsDevice.PresentMode is set to VSYNC, the framerate will be limited by the monitor refresh rate.
		/// </summary>
		public static FramePacingSettings CreateUncapped(
			int timestepFPS
		) {
			return new FramePacingSettings(
				FramePacingMode.Uncapped,
				timestepFPS,
				0
			);
		}

		private FramePacingSettings(
			FramePacingMode mode,
			int timestepFPS,
			int framerateCapFPS
		) {
			Mode = mode;
			Timestep = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / timestepFPS);

			if (mode != FramePacingMode.Uncapped)
			{
				FramerateCapTimestep = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / framerateCapFPS);
			}
			else
			{
				FramerateCapTimestep = TimeSpan.Zero;
			}
		}
	}
}
