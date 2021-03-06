﻿using System; using ComponentBind;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Lemma.Components;
using System.IO;
using Lemma.IO;
using Lemma.Util;

namespace Lemma.Factories
{
	public class NoteFactory : Factory<Main>
	{
		public NoteFactory()
		{
			this.Color = new Vector3(1.0f, 1.0f, 0.7f);
		}

		public override Entity Create(Main main)
		{
			Entity entity = new Entity(main, "Note");
			VoxelAttachable attachable = VoxelAttachable.MakeAttachable(entity, main);
			attachable.Vector.Value = Direction.NegativeY;
			return entity;
		}

		public override void Bind(Entity entity, Main main, bool creating = false)
		{
			Transform transform = entity.GetOrCreate<Transform>("Transform");
			PlayerTrigger trigger = entity.GetOrCreate<PlayerTrigger>();
			Model model = entity.GetOrCreate<Model>("Model");

			VoxelAttachable attachable = VoxelAttachable.MakeAttachable(entity, main);

			this.SetMain(entity, main);
			model.Serialize = false;
			model.Filename.Value = "Models\\papers";
			model.Add(new Binding<Matrix>(model.Transform, transform.Matrix));

			Note note = entity.GetOrCreate<Note>("Note");

			trigger.Serialize = false;
			trigger.Add(new Binding<Vector3>(trigger.Position, transform.Position));
			trigger.Radius.Value = 3.5f;

			trigger.Add(new CommandBinding(trigger.PlayerEntered, delegate()
			{
				PlayerFactory.Instance.Get<Player>().Note.Value = entity;
			}));

			trigger.Add(new CommandBinding(trigger.PlayerExited, delegate()
			{
				if (PlayerFactory.Instance != null)
					PlayerFactory.Instance.Get<Player>().Note.Value = null;
			}));

			entity.Add("Collected", note.Collected);
			entity.Add("Text", note.Text);
			entity.Add("Image", note.Image, new PropertyEntry.EditorData
			{
				Options = FileFilter.Get(main, main.Content.RootDirectory, new[] { "Images", Path.Combine(MapLoader.MapDirectory, "Images") }),
			});
			attachable.EditorProperties();
		}

		public override void AttachEditorComponents(Entity entity, Main main)
		{
			base.AttachEditorComponents(entity, main);
			Model editorModel = entity.Get<Model>("EditorModel");
			editorModel.Add(new Binding<bool>(editorModel.Enabled, () => Editor.EditorModelsVisible && !entity.EditorSelected, entity.EditorSelected, Editor.EditorModelsVisible));

			VoxelAttachable.AttachEditorComponents(entity, main, editorModel.Color);
			PlayerTrigger.AttachEditorComponents(entity, main, editorModel.Color);
		}
	}
}
