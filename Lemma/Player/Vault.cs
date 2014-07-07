﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ComponentBind;
using Lemma.Util;
using Microsoft.Xna.Framework;

namespace Lemma.Components
{
	public class Vault : Component<Main>, IUpdateableComponent
	{
		public enum State
		{
			None,
			Straight,
			Down,
		}

		private Random random = new Random();

		// Input
		public Property<Vector3> Position = new Property<Vector3>();
		public Property<Vector3> FloorPosition = new Property<Vector3>();
		public Property<float> MaxSpeed = new Property<float>();
		public Property<WallRun.State> WallRunState = new Property<WallRun.State>();

		// Output
		public Property<State> CurrentState = new Property<State>();
		public Command LockRotation = new Command();
		public Property<float> LastSupportedSpeed = new Property<float>();
		public Command DeactivateWallRun = new Command();
		public Command<WallRun.State> ActivateWallRun = new Command<WallRun.State>();
		public Command<float> FallDamage = new Command<float>();
		private AnimatedModel model;
		public Property<float> LastVaultStarted = new Property<float> { Value = -1.0f };

		// Input/output
		public BlockPredictor Predictor;
		public Property<float> Rotation = new Property<float>();
		public Property<bool> IsSupported = new Property<bool>();
		public Property<bool> HasTraction = new Property<bool>();
		public Property<bool> EnableWalking = new Property<bool>();
		public Property<bool> AllowUncrouch = new Property<bool>();
		public Property<bool> Crouched = new Property<bool>();
		public Property<Vector3> LinearVelocity = new Property<Vector3>();

		private float vaultTime;

		private bool vaultOver;
		private bool isTopOut;
		
		private float moveForwardStartTime;
		private bool movingForward;

		private float walkOffEdgeTimer;
		private Vector3 originalPosition;
		private Vector3 relativeVaultStartPosition;
		private Vector3 vaultVelocity;
		private float initialVerticalDifference;
		private Vector3 forward;
		private Voxel map;
		private Voxel.Coord coord;

		const float topOutVerticalSpeed = 4.5f;
		const float mantleVaultVerticalSpeed = 8.0f;
		const float maxVaultTime = 1.0f;
		const float maxTopoutTime = 2.0f;
		const int searchUpDistance = 2;
		const int searchDownDistance = 4;

		public void Bind(AnimatedModel model)
		{
			this.model = model;

			// Filters are in Blender's Z-up coordinate system

			this.model["Mantle"].Speed = 1.3f;
			this.model["Mantle"].GetChannel(this.model.GetBoneIndex("ORG-hips")).Filter = delegate(Matrix m)
			{
				m.Translation = new Vector3(0.0f, 0.0f, 2.0f);
				return m;
			};
			this.model["TopOut"].Speed = 1.8f;
			this.model["TopOut"].GetChannel(this.model.GetBoneIndex("ORG-hips")).Filter = delegate(Matrix m)
			{
				Vector3 diff = Vector3.Transform(this.relativeVaultStartPosition, this.map.Transform) + new Vector3(0, 0.535f, 0) - this.Position;
				m.Translation += Vector3.Transform(diff, Matrix.CreateRotationY(-this.Rotation) * Matrix.CreateRotationX((float)Math.PI * 0.5f));
				return m;
			};
			this.model["Vault"].Speed = 1.3f;
			this.model["Vault"].GetChannel(this.model.GetBoneIndex("ORG-hips")).Filter = delegate(Matrix m)
			{
				m.Translation += (Matrix.CreateRotationY(-this.Rotation) * Matrix.CreateRotationX((float)Math.PI * 0.5f) * Matrix.CreateTranslation(0, -1.0f + this.vaultTime * 1.0f, this.initialVerticalDifference - 2.0f - this.vaultTime * 3.0f)).Translation;
				return m;
			};
		}

		public override void Awake()
		{
			base.Awake();
			this.Serialize = false;
			this.EnabledWhenPaused = false;
		}

		public bool Go()
		{
			if (this.main.TotalTime - this.LastVaultStarted < vaultCoolDown)
				return false;

			Matrix rotationMatrix = Matrix.CreateRotationY(this.Rotation);
			foreach (Voxel map in Voxel.ActivePhysicsVoxels)
			{
				Direction up = map.GetRelativeDirection(Direction.PositiveY);
				Direction backward = map.GetRelativeDirection(rotationMatrix.Forward);
				Direction right = up.Cross(backward);
				Vector3 pos = this.Position + rotationMatrix.Forward * -1.75f;
				Voxel.Coord baseCoord = map.GetCoordinate(pos).Move(up, searchUpDistance);
				foreach (int x in new[] { 0, -1, 1 })
				{
					Voxel.Coord coord = baseCoord.Move(right, x);
					for (int i = 0; i < searchDownDistance; i++)
					{
						if (map[coord] != Voxel.EmptyState)
						{
							if (map[coord.Move(backward)] != Voxel.EmptyState
								|| map[coord.Move(up)] != Voxel.EmptyState
								|| map[coord.Move(up, 2)] != Voxel.EmptyState
								|| map[coord.Move(up, 3)] != Voxel.EmptyState
								|| map[coord.Move(up).Move(backward)] != Voxel.EmptyState
								|| map[coord.Move(up, 2).Move(backward)] != Voxel.EmptyState
								|| map[coord.Move(up, 3).Move(backward)] != Voxel.EmptyState)
								break; // Conflict

							// Vault
							this.vault(map, coord.Move(up));
							return true;
						}
						coord = coord.Move(up.GetReverse());
					}
				}
			}

			// Check block possibilities for vaulting
			foreach (BlockPredictor.Possibility possibility in this.Predictor.AllPossibilities)
			{
				Direction up = possibility.Map.GetRelativeDirection(Direction.PositiveY);
				Direction right = possibility.Map.GetRelativeDirection(Vector3.Cross(Vector3.Up, -rotationMatrix.Forward));
				Vector3 pos = this.Position + rotationMatrix.Forward * (this.WallRunState == WallRun.State.Straight ? -1.75f : -1.25f);
				Voxel.Coord baseCoord = possibility.Map.GetCoordinate(pos).Move(up, searchUpDistance);
				foreach (int x in new[] { 0, -1, 1 })
				{
					Voxel.Coord coord = baseCoord.Move(right, x);
					for (int i = 0; i < searchDownDistance; i++)
					{
						Voxel.Coord downCoord = coord.Move(up.GetReverse());
						if (!coord.Between(possibility.StartCoord, possibility.EndCoord) && downCoord.Between(possibility.StartCoord, possibility.EndCoord))
						{
							this.Predictor.InstantiatePossibility(possibility);
							this.vault(possibility.Map, coord);
							return true;
						}
						coord = coord.Move(up.GetReverse());
					}
				}
			}

			return false;
		}

		private void vault(Voxel map, Voxel.Coord coord)
		{
			DynamicVoxel dynamicMap = map as DynamicVoxel;
			Vector3 supportVelocity = Vector3.Zero;

			if (dynamicMap != null)
			{
				BEPUphysics.Entities.Entity supportEntity = dynamicMap.PhysicsEntity;
				Vector3 supportLocation = this.FloorPosition;
				supportVelocity = supportEntity.LinearVelocity + Vector3.Cross(supportEntity.AngularVelocity, supportLocation - supportEntity.Position);
			}

			float verticalVelocityChange = this.LinearVelocity.Value.Y - supportVelocity.Y;
			this.FallDamage.Execute(verticalVelocityChange);
			if (!this.Active) // We died from fall damage
				return;

			this.DeactivateWallRun.Execute();
			this.CurrentState.Value = State.Straight;

			this.coord = coord;

			Vector3 coordPosition = map.GetAbsolutePosition(coord);
			this.forward = coordPosition - this.Position;
			this.initialVerticalDifference = forward.Y;

			this.isTopOut = this.initialVerticalDifference > 1.75f || verticalVelocityChange < Lemma.Components.FallDamage.DamageVelocity;

			// Grunt if we're going up
			// If we're falling down, don't grunt because we might already be grunting from the fall damage
			// That would just be awkward
			if (this.random.NextDouble() > 0.5 && verticalVelocityChange >= 0)
				AkSoundEngine.PostEvent(AK.EVENTS.PLAY_PLAYER_GRUNT, this.Entity);

			this.forward.Y = 0.0f;

			float horizontalDistanceToCoord = this.forward.Length();
			this.forward /= horizontalDistanceToCoord;
			if (horizontalDistanceToCoord < Character.DefaultRadius + 1.0f)
			{
				Vector3 pos = coordPosition + this.forward * (Character.DefaultRadius + 1.0f);
				pos.Y = this.Position.Value.Y;
				this.Position.Value = pos;
			}

			// If there's nothing on the other side of the wall (it's a one-block-wide wall)
			// then vault over it rather than standing on top of it
			this.vaultOver = map[coordPosition + this.forward + Vector3.Down].ID == 0;
			if (this.vaultOver)
				this.isTopOut = false; // Don't do a top out animation if we're going to vault over it

			this.vaultVelocity = supportVelocity + new Vector3(0, this.isTopOut ? topOutVerticalSpeed : mantleVaultVerticalSpeed, 0);

			this.map = map;

			this.LinearVelocity.Value = this.vaultVelocity;
			this.IsSupported.Value = false;
			this.HasTraction.Value = false;

			Direction relativeDir = map.GetRelativeDirection(this.forward);
			Vector3 absoluteDirVector = map.GetAbsoluteVector(relativeDir.GetVector());
			this.Rotation.Value = (float)Math.Atan2(absoluteDirVector.X, absoluteDirVector.Z);
			this.LockRotation.Execute();

			this.EnableWalking.Value = false;
			this.Crouched.Value = true;
			this.AllowUncrouch.Value = false;

			Session.Recorder.Event(main, "Vault");
			this.model.Stop
			(
				"Vault",
				"Mantle",
				"TopOut",
				"Jump",
				"Jump02",
				"Jump03",
				"JumpLeft",
				"JumpRight",
				"JumpBackward"
			);
			this.model.StartClip(this.vaultOver ? "Vault" : (this.isTopOut ? "TopOut" : "Mantle"), 4, false, AnimatedModel.DefaultBlendTime);

			this.vaultTime = 0.0f;
			this.moveForwardStartTime = 0.0f;
			this.movingForward = false;
			this.originalPosition = this.Position;

			// If this is a top-out, we have to make sure the animation lines up perfectly
			if (this.isTopOut)
			{
				Direction up = map.GetRelativeDirection(Direction.PositiveY);
				this.relativeVaultStartPosition = map.GetRelativePosition(coord.Move(relativeDir, -2)) + up.GetVector() * -3.7f;
			}
			else
				this.relativeVaultStartPosition = Vector3.Transform(this.originalPosition, Matrix.Invert(this.map.Transform));
			
			this.LastVaultStarted.Value = this.main.TotalTime;
		}

		private void vaultDown(Vector3 forward)
		{
			this.forward = forward;
			this.vaultVelocity = this.forward * this.MaxSpeed;
			this.vaultVelocity.Y = this.LinearVelocity.Value.Y;
			this.LinearVelocity.Value = this.vaultVelocity;
			this.LockRotation.Execute();
			this.EnableWalking.Value = false;
			this.Crouched.Value = true;
			this.AllowUncrouch.Value = false;
			this.walkOffEdgeTimer = 0.0f;

			this.vaultTime = 0.0f;
			this.CurrentState.Value = State.Down;

			this.originalPosition = this.Position;
			this.LastVaultStarted.Value = this.main.TotalTime;
		}

		private const float vaultCoolDown = 0.5f;

		public bool TryVaultDown()
		{
			if (this.Crouched || !this.IsSupported || this.main.TotalTime - this.LastVaultStarted < vaultCoolDown)
				return false;

			Matrix rotationMatrix = Matrix.CreateRotationY(this.Rotation);
			bool foundObstacle = false;
			foreach (Voxel map in Voxel.ActivePhysicsVoxels)
			{
				Direction down = map.GetRelativeDirection(Direction.NegativeY);
				Vector3 pos = this.Position + rotationMatrix.Forward * -1.75f;
				Voxel.Coord coord = map.GetCoordinate(pos);

				for (int i = 0; i < 5; i++)
				{
					if (map[coord].ID != 0)
					{
						foundObstacle = true;
						break;
					}
					coord = coord.Move(down);
				}

				if (foundObstacle)
					break;
			}

			if (!foundObstacle)
			{
				// Vault
				this.vaultDown(-rotationMatrix.Forward);
			}
			return !foundObstacle;
		}

		public void Update(float dt)
		{
			if (this.CurrentState == State.Down)
			{
				this.vaultTime += dt;

				bool delete = false;

				if (this.vaultTime > (this.isTopOut ? maxTopoutTime : maxVaultTime)) // Max vault time ensures we never get stuck
					delete = true;
				else if (this.walkOffEdgeTimer > 0.2f && this.IsSupported)
					delete = true; // We went over the edge and hit the ground. Stop.
				else if (!this.IsSupported) // We hit the edge, go down it
				{
					this.walkOffEdgeTimer += dt;

					if (this.walkOffEdgeTimer > 0.1f)
					{
						this.LinearVelocity.Value = new Vector3(0, -mantleVaultVerticalSpeed, 0);

						if (this.Position.Value.Y < this.originalPosition.Y - 3.0f)
							delete = true;
						else
						{
							this.ActivateWallRun.Execute(WallRun.State.Reverse);
							if (this.WallRunState.Value == WallRun.State.Reverse)
								delete = true;
						}
					}
				}

				if (this.walkOffEdgeTimer < 0.1f)
				{
					Vector3 velocity = this.forward * this.MaxSpeed;
					velocity.Y = this.LinearVelocity.Value.Y;
					this.LinearVelocity.Value = velocity;
				}

				if (delete)
				{
					this.AllowUncrouch.Value = true;
					this.EnableWalking.Value = true;
					this.CurrentState.Value = State.None;
				}
			}
			else if (this.CurrentState != State.None)
			{
				this.vaultTime += dt;

				bool delete = false;

				if (this.movingForward)
				{
					if (this.vaultOver && this.vaultTime - this.moveForwardStartTime > 0.25f)
						delete = true; // Done moving forward
					else if (this.isTopOut && !this.model.IsPlaying("TopOut"))
						delete = true;
					else
					{
						// Still moving forward
						this.LinearVelocity.Value = this.forward * (this.isTopOut ? this.MaxSpeed * 0.5f : this.MaxSpeed);
						this.LastSupportedSpeed.Value = this.MaxSpeed;
					}
				}
				else
				{
					// We're still going up.
					if (this.LinearVelocity.Value.Y < 0.0f)
					{
						// We hit something above us.
						delete = true;
					}
					else if (this.IsSupported || this.vaultTime > (this.isTopOut ? maxTopoutTime : maxVaultTime)
						|| (this.FloorPosition.Value.Y > this.map.GetAbsolutePosition(this.coord).Y + (this.vaultOver ? 0.2f : 0.1f))) // Move forward
					{
						// We've reached the top of the vault. Start moving forward.
						// Max vault time ensures we never get stuck

						if (this.isTopOut || this.vaultOver)
						{
							// We need to keep the vault mover alive for a while
							// to keep the player moving forward over the wall
							this.movingForward = true;
							this.moveForwardStartTime = this.vaultTime;
						}
						else
						{
							// It's just a mantle, we're done
							this.LinearVelocity.Value = this.forward * this.MaxSpeed;
							this.LastSupportedSpeed.Value = this.MaxSpeed;
							delete = true;
						}
					}
					else // We're still going up.
						this.LinearVelocity.Value = vaultVelocity;
				}

				if (delete)
				{
					this.CurrentState.Value = State.None;
					this.EnableWalking.Value = true;
					this.Entity.Add(new Animation
					(
						new Animation.Delay(0.1f),
						new Animation.Set<bool>(this.AllowUncrouch, true)
					));
				}
			}
			else if (this.map != null && !this.model.IsPlaying("Vault", "TopOut", "Mantle"))
				this.map = null;
		}
	}
}
