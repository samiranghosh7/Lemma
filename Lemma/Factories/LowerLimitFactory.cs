﻿using System; using ComponentBind;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Lemma.Components;

namespace Lemma.Factories
{
	public class LowerLimitFactory : Factory<Main>
	{
		public LowerLimitFactory()
		{
			this.Color = new Vector3(0.4f, 0.4f, 0.4f);
		}

		public override Entity Create(Main main)
		{
			return new Entity(main, "LowerLimit");
		}

		const float absoluteLimit = -20.0f;
		const float velocityThreshold = -40.0f;

		public override void Bind(Entity entity, Main main, bool creating = false)
		{
			Transform transform = entity.GetOrCreate<Transform>("Transform");
			this.SetMain(entity, main);
			entity.CannotSuspendByDistance = true;
			transform.Editable = true;
			if (entity.GetOrMakeProperty<bool>("Attach", true))
				MapAttachable.MakeAttachable(entity, main);
			
			entity.Add(new Updater
			{
				delegate(float dt)
				{
					Entity player = PlayerFactory.Instance;
					if (player != null && player.Active)
					{
						Player p = player.Get<Player>();
						float y = p.Character.Transform.Value.Translation.Y;
						float limit = transform.Position.Value.Y;
						if (y < limit + absoluteLimit || (y < limit && p.Character.LinearVelocity.Value.Y < velocityThreshold))
							player.Delete.Execute();
					}
					else
						player = null;
				}
			});
		}

		public override void AttachEditorComponents(Entity entity, Main main)
		{
			base.AttachEditorComponents(entity, main);

			MapAttachable.AttachEditorComponents(entity, main, entity.Get<Model>().Color);
		}
	}
}