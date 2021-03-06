﻿using System; using ComponentBind;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Lemma.Components;
using Microsoft.Xna.Framework.Graphics;
using System.IO;

namespace Lemma.Factories
{
	public class SkyboxFactory : Factory<Main>
	{
		public SkyboxFactory()
		{
			this.Color = new Vector3(0.8f, 0.6f, 0.4f);
		}

		public override Entity Create(Main main)
		{
			Entity entity = new Entity(main, "Skybox");

			ModelAlpha skybox = new ModelAlpha();
			skybox.DiffuseTexture.Value = "Skyboxes\\skybox-sun";
			entity.Add("Skybox", skybox);

			return entity;
		}

		public override void Bind(Entity entity, Main main, bool creating = false)
		{
			Transform transform = entity.GetOrCreate<Transform>("Transform");

			Skybox skybox = entity.GetOrCreate<Skybox>("Settings");

			ModelAlpha model = entity.Get<ModelAlpha>("Skybox");
			model.Filename.Value = "InternalModels\\skybox";
			base.Bind(entity, main, creating);
			entity.CannotSuspendByDistance = true;

			model.DisableCulling.Value = true;
			model.CullBoundingBox.Value = false;
			model.Add(new Binding<Matrix>(model.Transform, transform.Matrix));
			model.DrawOrder.Value = -10;

			model.Add
			(
				new Binding<string>
				(
					model.TechniquePostfix,
					delegate()
					{
						string prefix = "";
						if (Water.BigWaterShader)
							prefix += "Water";
						else if (skybox.Vertical)
							prefix += "Vertical";

						if (main.LightingManager.HasGlobalShadowLight
							&& main.LightingManager.DynamicShadows != LightingManager.DynamicShadowSetting.Off
							&& skybox.GodRays > 0.0f
							&& main.Settings.VolumetricLighting)
							prefix += "GodRays";
						return prefix;
					},
					skybox.Vertical, main.LightingManager.HasGlobalShadowLight, main.LightingManager.DynamicShadows, skybox.GodRays, main.Settings.VolumetricLighting, Water.BigWaterShader
				)
			);
			model.Add(new Binding<float>(model.GetFloatParameter("WaterHeight"), Water.BigWaterHeight));
			model.Add(new Binding<float>(model.GetFloatParameter("VerticalSize"), skybox.VerticalSize));
			model.Add(new Binding<float>(model.GetFloatParameter("VerticalCenter"), skybox.VerticalCenter));
			model.Add(new Binding<float>(model.GetFloatParameter("GodRayStrength"), skybox.GodRays));
			model.Add(new Binding<float>(model.GetFloatParameter("GodRayExtinction"), skybox.GodRayExtinction));
			model.Add(new Binding<Vector3>(model.GetVector3Parameter("CameraPosition"), main.Camera.Position));
			model.Add(new Binding<RenderTarget2D>(model.GetRenderTarget2DParameter("ShadowMap" + Components.Model.SamplerPostfix), () => main.LightingManager.GlobalShadowMap, main.LightingManager.GlobalShadowMap, main.ScreenSize));
			model.Add(new Binding<Matrix>(model.GetMatrixParameter("ShadowViewProjectionMatrix"), main.LightingManager.GlobalShadowViewProjection));
			model.GetTexture2DParameter("Random" + Lemma.Components.Model.SamplerPostfix).Value = main.Content.Load<Texture2D>("Textures\\random");

			model.Add(new Binding<float>(model.GetFloatParameter("StartDistance"), skybox.StartDistance));

			entity.Add("Color", model.Color, new PropertyEntry.EditorData(){FChangeBy = 0.1f});
			entity.Add("Vertical", skybox.Vertical);
			entity.Add("VerticalSize", skybox.VerticalSize);
			entity.Add("VerticalCenter", skybox.VerticalCenter);
			entity.Add("GodRays", skybox.GodRays);
			entity.Add("GodRayExtinction", skybox.GodRayExtinction);
			entity.Add("StartDistance", skybox.StartDistance);
			entity.Add("Image", model.DiffuseTexture, new PropertyEntry.EditorData
			{
				Options = FileFilter.Get(main, main.Content.RootDirectory, new[] { "Skyboxes" }),
			});
		}
	}
}