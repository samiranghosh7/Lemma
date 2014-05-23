﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ComponentBind;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Lemma.Components
{
	public class PhoneNote
	{
		public static void Attach(Main main, Entity entity, AnimatedModel model, FPSInput input, Phone phone, Property<bool> enableWalking, Property<bool> enableMoves)
		{
			Property<bool> phoneActive = entity.GetOrMakeProperty<bool>("PhoneActive");
			Property<bool> noteActive = entity.GetOrMakeProperty<bool>("NoteActive");

			UIRenderer phoneUi = entity.GetOrCreate<UIRenderer>("PhoneUI");

			Property<Entity.Handle> signalTower = entity.GetOrMakeProperty<Entity.Handle>("SignalTower");

			const float phoneWidth = 200.0f;

			phoneUi.RenderTargetBackground.Value = Microsoft.Xna.Framework.Color.White;
			phoneUi.RenderTargetSize.Value = new Point((int)phoneWidth, (int)(phoneWidth * 2.0f));
			phoneUi.Serialize = false;
			phoneUi.Enabled.Value = false;

			Model phoneModel = entity.GetOrCreate<Model>("PhoneModel");
			phoneModel.Filename.Value = "Models\\phone";
			phoneModel.Color.Value = new Vector3(0.13f, 0.13f, 0.13f);
			phoneModel.Serialize = false;
			phoneModel.Enabled.Value = false;

			Property<Matrix> phoneBone = model.GetBoneTransform("Phone");
			phoneModel.Add(new Binding<Matrix>(phoneModel.Transform, () => phoneBone.Value * model.Transform, phoneBone, model.Transform));

			Model screen = entity.GetOrCreate<Model>("Screen");
			screen.Filename.Value = "Models\\plane";
			screen.Add(new Binding<Microsoft.Xna.Framework.Graphics.RenderTarget2D>(screen.GetRenderTarget2DParameter("Diffuse" + Model.SamplerPostfix), phoneUi.RenderTarget));
			screen.Add(new Binding<Matrix>(screen.Transform, x => Matrix.CreateTranslation(0.015f, 0.0f, 0.0f) * x, phoneModel.Transform));
			screen.Serialize = false;
			screen.Enabled.Value = false;

			PointLight phoneLight = entity.GetOrCreate<PointLight>("PhoneLight");
			phoneLight.Serialize = false;
			phoneLight.Enabled.Value = false;
			phoneLight.Attenuation.Value = 0.5f;
			phoneLight.Add(new Binding<Vector3, Matrix>(phoneLight.Position, x => x.Translation, screen.Transform));

			const float screenScale = 0.0007f;
			screen.Scale.Value = new Vector3(1.0f, (float)phoneUi.RenderTargetSize.Value.Y * screenScale, (float)phoneUi.RenderTargetSize.Value.X * screenScale);

			// Transform screen space mouse position into 3D, then back into the 2D space of the phone UI
			Property<Matrix> screenTransform = new Property<Matrix>();
			screen.Add(new Binding<Matrix>(screenTransform, () => Matrix.CreateScale(screen.Scale) * screen.Transform, screen.Scale, screen.Transform));
			phoneUi.Setup3D(screenTransform);

			// Phone UI

			const float padding = 8.0f;
			const float messageWidth = phoneWidth - padding * 2.0f;

			Func<Color, string, float, Container> makeButton = delegate(Color color, string text, float width)
			{
				Container bg = new Container();
				bg.Tint.Value = color;
				bg.PaddingBottom.Value = bg.PaddingLeft.Value = bg.PaddingRight.Value = bg.PaddingTop.Value = padding * 0.5f;
				Color highlightColor = new Color(color.ToVector4() + new Vector4(0.2f, 0.2f, 0.2f, 0.0f));
				bg.Add(new Binding<Color, bool>(bg.Tint, x => x ? highlightColor : color, bg.Highlighted));

				TextElement msg = new TextElement();
				msg.Name.Value = "Text";
				msg.FontFile.Value = "Font";
				msg.Text.Value = text;
				msg.WrapWidth.Value = width;
				bg.Children.Add(msg);
				return bg;
			};

			Func<UIComponent, bool, Container> makeAlign = delegate(UIComponent component, bool right)
			{
				Container container = new Container();
				container.Opacity.Value = 0.0f;
				container.PaddingBottom.Value = container.PaddingLeft.Value = container.PaddingRight.Value = container.PaddingTop.Value = 0.0f;
				container.ResizeHorizontal.Value = false;
				container.Size.Value = new Vector2(messageWidth, 0.0f);
				component.AnchorPoint.Value = new Vector2(right ? 1.0f : 0.0f, 0.0f);
				component.Position.Value = new Vector2(right ? messageWidth : 0.0f, 0.0f);
				container.Children.Add(component);
				return container;
			};

			Color incomingColor = new Color(0.0f, 0.0f, 0.0f, 1.0f);
			Color outgoingColor = new Color(0.0f, 0.175f, 0.35f, 1.0f);

			Container topBarContainer = new Container();
			topBarContainer.ResizeHorizontal.Value = false;
			topBarContainer.Size.Value = new Vector2(phoneUi.RenderTargetSize.Value.X, 0.0f);
			topBarContainer.Tint.Value = new Color(0.15f, 0.15f, 0.15f, 1.0f);
			phoneUi.Root.Children.Add(topBarContainer);

			ListContainer phoneTopBar = new ListContainer();
			phoneTopBar.Orientation.Value = ListContainer.ListOrientation.Horizontal;
			phoneTopBar.Spacing.Value = padding;
			topBarContainer.Children.Add(phoneTopBar);

			Sprite signalIcon = new Sprite();
			signalIcon.Image.Value = "Images\\signal";
			phoneTopBar.Children.Add(signalIcon);

			TextElement noService = new TextElement();
			noService.FontFile.Value = "Font";
			noService.Text.Value = "\\no service";
			phoneTopBar.Children.Add(noService);

			signalIcon.Add(new Binding<bool, Entity.Handle>(signalIcon.Visible, x => x.Target != null && x.Target.Active, signalTower));
			noService.Add(new Binding<bool, Entity.Handle>(noService.Visible, x => x.Target == null || !x.Target.Active, signalTower));

			ListContainer phoneLayout = new ListContainer();
			phoneLayout.Spacing.Value = padding;
			phoneLayout.Orientation.Value = ListContainer.ListOrientation.Vertical;
			phoneLayout.Add(new Binding<Vector2>(phoneLayout.Position, x => new Vector2(padding, x.Y), topBarContainer.Size));
			phoneLayout.Add(new Binding<Vector2>(phoneLayout.Size, () => new Vector2(phoneUi.RenderTargetSize.Value.X - padding * 2.0f, phoneUi.RenderTargetSize.Value.Y - padding - topBarContainer.Size.Value.Y), phoneUi.RenderTargetSize, topBarContainer.Size));
			phoneUi.Root.Children.Add(phoneLayout);

			Container composeButton = makeButton(new Color(0.5f, 0.0f, 0.0f, 1.0f), "\\compose", messageWidth - padding * 2.0f);
			TextElement composeText = (TextElement)composeButton.GetChildByName("Text");
			composeText.Add(new Binding<string, bool>(composeText.Text, x => x ? "\\compose gamepad" : "\\compose", main.GamePadConnected));
			UIComponent composeAlign = makeAlign(composeButton, true);

			Scroller phoneScroll = new Scroller();
			phoneScroll.ResizeVertical.Value = false;
			phoneScroll.Add(new Binding<Vector2>(phoneScroll.Size, () => new Vector2(phoneLayout.Size.Value.X, phoneLayout.Size.Value.Y - phoneLayout.Spacing.Value - composeAlign.ScaledSize.Value.Y), phoneLayout.Size, phoneLayout.Spacing, composeAlign.ScaledSize));

			phoneLayout.Children.Add(phoneScroll);
			phoneLayout.Children.Add(composeAlign);

			ListContainer msgList = new ListContainer();
			msgList.Spacing.Value = padding * 0.5f;
			msgList.Orientation.Value = ListContainer.ListOrientation.Vertical;
			msgList.ResizePerpendicular.Value = false;
			msgList.Size.Value = new Vector2(messageWidth, 0.0f);
			phoneScroll.Children.Add(msgList);

			Container answerContainer = new Container();
			answerContainer.PaddingBottom.Value = answerContainer.PaddingLeft.Value = answerContainer.PaddingRight.Value = answerContainer.PaddingTop.Value = padding;
			answerContainer.Tint.Value = incomingColor;
			answerContainer.AnchorPoint.Value = new Vector2(1.0f, 1.0f);
			answerContainer.Add(new Binding<Vector2>(answerContainer.Position, () => composeAlign.Position.Value + new Vector2(composeAlign.ScaledSize.Value.X + padding, padding * 3.0f), composeAlign.Position, composeAlign.ScaledSize));
			phoneUi.Root.Children.Add(answerContainer);
			answerContainer.Visible.Value = false;

			ListContainer answerList = new ListContainer();
			answerList.Orientation.Value = ListContainer.ListOrientation.Vertical;
			answerList.Alignment.Value = ListContainer.ListAlignment.Max;
			answerContainer.Children.Add(answerList);

			int selectedAnswer = 0;

			composeButton.Add(new CommandBinding<Point>(composeButton.MouseLeftUp, delegate(Point p)
			{
				answerContainer.Visible.Value = !answerContainer.Visible;
				if (answerContainer.Visible && main.GamePadConnected)
				{
					selectedAnswer = 0;
					foreach (UIComponent answer in answerList.Children)
						answer.Highlighted.Value = false;
					answerList.Children[0].Highlighted.Value = true;
				}
			}));

			Action scrollToBottom = delegate()
			{
				// HACK
				main.AddComponent(new Animation
				(
					new Animation.Delay(0.01f),
					new Animation.Execute(delegate()
					{
						phoneScroll.ScrollToBottom();
					})
				));
			};

			// Note

			UIRenderer noteUi = entity.GetOrCreate<UIRenderer>("NoteUI");

			const float noteWidth = 400.0f;
			const float noteScale = 0.0009f;

			noteUi.RenderTargetBackground.Value = new Microsoft.Xna.Framework.Color(1.0f, 0.95f, 0.9f);
			noteUi.RenderTargetSize.Value = new Point((int)noteWidth, (int)(noteWidth * 1.29f)); // 8.5x11 aspect ratio
			noteUi.Serialize = false;
			noteUi.Enabled.Value = false;

			Model noteModel = entity.GetOrCreate<Model>("Note");
			noteModel.Filename.Value = "Models\\plane";
			noteModel.EffectFile.Value = "Effects\\Default";
			noteModel.Add(new Binding<Microsoft.Xna.Framework.Graphics.RenderTarget2D>(noteModel.GetRenderTarget2DParameter("Diffuse" + Model.SamplerPostfix), noteUi.RenderTarget));
			noteModel.Add(new Binding<Matrix>(noteModel.Transform, x => Matrix.CreateTranslation(-0.005f, 0.05f, 0.08f) * x, phoneModel.Transform));
			noteModel.Scale.Value = new Vector3(1.0f, (float)noteUi.RenderTargetSize.Value.Y * noteScale, (float)noteUi.RenderTargetSize.Value.X * noteScale);
			noteModel.Serialize = false;
			noteModel.Enabled.Value = false;
			Property<Entity.Handle> note = entity.GetOrMakeProperty<Entity.Handle>("Note");

			Container togglePhoneMessage = null;

			entity.Add(new NotifyBinding(delegate()
			{
				bool hasNoteOrSignalTower = (note.Value.Target != null && note.Value.Target.Active)
					|| (signalTower.Value.Target != null && signalTower.Value.Target.Active);

				if (togglePhoneMessage == null && hasNoteOrSignalTower)
					togglePhoneMessage = ((GameMain)main).Menu.ShowMessage(entity, "[{{TogglePhone}}]");
				else if (togglePhoneMessage != null && !hasNoteOrSignalTower && !phoneActive && !noteActive)
				{
					((GameMain)main).Menu.HideMessage(entity, togglePhoneMessage);
					togglePhoneMessage = null;
				}
			}, note, signalTower));

			entity.Add(new CommandBinding(entity.Delete, delegate()
			{
				((GameMain)main).Menu.HideMessage(null, togglePhoneMessage);
			}));

			// Note UI

			const float notePadding = 40.0f;

			ListContainer noteLayout = new ListContainer();
			noteLayout.Spacing.Value = padding;
			noteLayout.Orientation.Value = ListContainer.ListOrientation.Vertical;
			noteLayout.Alignment.Value = ListContainer.ListAlignment.Min;
			noteLayout.Position.Value = new Vector2(notePadding, notePadding);
			noteLayout.Add(new Binding<Vector2, Point>(noteLayout.Size, x => new Vector2(x.X - notePadding * 2.0f, x.Y - notePadding * 2.0f), noteUi.RenderTargetSize));
			noteUi.Root.Children.Add(noteLayout);

			Sprite noteUiImage = new Sprite();
			noteLayout.Children.Add(noteUiImage);

			TextElement noteUiText = new TextElement();
			noteUiText.FontFile.Value = "Font";
			noteUiText.Tint.Value = new Microsoft.Xna.Framework.Color(0.1f, 0.1f, 0.1f);
			noteUiText.Add(new Binding<float, Vector2>(noteUiText.WrapWidth, x => x.X, noteLayout.Size));
			noteLayout.Children.Add(noteUiText);

			// Toggle note

			Action<bool> showNote = delegate(bool show)
			{
				noteActive.Value = show;
				input.EnableLook.Value = input.EnableMouse.Value = !noteActive;
				main.IsMouseVisible.Value = false;
				enableWalking.Value = enableMoves.Value = !noteActive;
				noteModel.Enabled.Value = noteActive;
				noteUi.Enabled.Value = noteActive;

				model.Stop("Phone", "Note");
				Entity noteEntity = note.Value.Target;
				if (noteEntity != null && noteEntity.Active)
				{
					if (noteActive)
					{
						noteUiImage.Image.Value = noteEntity.GetOrMakeProperty<string>("Image");
						noteUiText.Text.Value = noteEntity.GetOrMakeProperty<string>("Text");
						model.StartClip("Note", 6, true, AnimatedModel.DefaultBlendTime * 2.0f);
						float startRotationY = input.Mouse.Value.Y;
						// Level the player's view
						entity.Add(new Animation
						(
							new Animation.Ease
							(
								new Animation.Custom(delegate(float x)
								{
									input.Mouse.Value = new Vector2(input.Mouse.Value.X, startRotationY * (1.0f - x));
								}, 0.5f),
								Animation.Ease.Type.OutQuadratic
							)
						));
					}
					else
					{
						Property<bool> collected = noteEntity.GetOrMakeProperty<bool>("Collected");
						if (!collected)
							collected.Value = true;
					}
				}
			};

			// Toggle phone

			Container phoneTutorialMessage = null;

			Action<bool> showPhone = delegate(bool show)
			{
				if (togglePhoneMessage != null)
				{
					((GameMain)main).Menu.HideMessage(entity, togglePhoneMessage);
					togglePhoneMessage = null;
				}

				if (phoneTutorialMessage != null)
				{
					((GameMain)main).Menu.HideMessage(entity, phoneTutorialMessage);
					phoneTutorialMessage = null;
				}

				if (show || (phone.Schedules.Count == 0 && !phone.WaitForAnswer))
				{
					phoneActive.Value = show;
					input.EnableLook.Value = input.EnableMouse.Value = !phoneActive;
					main.IsMouseVisible.Value = phoneActive;
					enableWalking.Value = enableMoves.Value = !phoneActive;
					phoneModel.Enabled.Value = phoneActive;
					screen.Enabled.Value = phoneActive;
					phoneUi.Enabled.Value = phoneActive;
					phoneLight.Enabled.Value = phoneActive;
					answerContainer.Visible.Value = false;

					model.Stop("Phone", "Note");
					if (phoneActive)
					{
						if (!phone.TutorialShown)
						{
							phone.TutorialShown.Value = true;
							phoneTutorialMessage = ((GameMain)main).Menu.ShowMessage(entity, "\\scroll for more");
						}
						phoneScroll.CheckLayout();
						scrollToBottom();

						model.StartClip("Phone", 6, true, AnimatedModel.DefaultBlendTime * 2.0f);

						// Level the player's view
						float startRotationY = input.Mouse.Value.Y;
						entity.Add(new Animation
						(
							new Animation.Ease
							(
								new Animation.Custom(delegate(float x)
								{
									input.Mouse.Value = new Vector2(input.Mouse.Value.X, startRotationY * (1.0f - x));
								}, 0.5f),
								Animation.Ease.Type.OutQuadratic
							)
						));
					}
				}
			};

			input.Bind(((GameMain)main).Settings.TogglePhone, PCInput.InputState.Up, delegate()
			{
				if (noteActive || phoneActive || phone.CanReceiveMessages)
				{
					if (!phoneActive && (noteActive || note.Value.Target != null && note.Value.Target.Active))
						showNote(!noteActive);
					else if (phone.Enabled)
						showPhone(!phoneActive);
				}
			});

			// Gamepad code for the phone

			input.Add(new CommandBinding(input.GetButtonUp(Buttons.A), () => phoneActive && composeButton.Visible, delegate()
			{
				if (answerContainer.Visible)
					answerList.Children[selectedAnswer].MouseLeftUp.Execute(new Point());
				else
					answerContainer.Visible.Value = true;
			}));

			input.Add(new CommandBinding(input.GetButtonUp(Buttons.B), () => phoneActive && answerContainer.Visible, delegate()
			{
				answerContainer.Visible.Value = false;
			}));

			Action<int> scrollPhone = delegate(int delta)
			{
				if (answerContainer.Visible)
				{
					answerList.Children[selectedAnswer].Highlighted.Value = false;
					selectedAnswer += delta;
					while (selectedAnswer < 0)
						selectedAnswer += answerList.Children.Count;
					while (selectedAnswer > answerList.Children.Count - 1)
						selectedAnswer -= answerList.Children.Count;
					answerList.Children[selectedAnswer].Highlighted.Value = true;
				}
				else
					phoneScroll.MouseScrolled.Execute(new Point(), delta * -4);
			};

			input.Add(new CommandBinding(input.GetButtonDown(Buttons.LeftThumbstickUp), () => phoneActive, delegate()
			{
				scrollPhone(-1);
			}));

			input.Add(new CommandBinding(input.GetButtonDown(Buttons.DPadUp), () => phoneActive, delegate()
			{
				scrollPhone(-1);
			}));

			input.Add(new CommandBinding(input.GetButtonDown(Buttons.LeftThumbstickDown), () => phoneActive, delegate()
			{
				scrollPhone(1);
			}));

			input.Add(new CommandBinding(input.GetButtonDown(Buttons.DPadDown), () => phoneActive, delegate()
			{
				scrollPhone(1);
			}));

			msgList.Add(new ListBinding<UIComponent, Phone.Message>
			(
				msgList.Children,
				phone.Messages,
				delegate(Phone.Message msg)
				{
					return makeAlign(makeButton(msg.Incoming ? incomingColor : outgoingColor, "\\" + (msg.Text == null ? msg.ID : msg.Text), messageWidth - padding * 2.0f), !msg.Incoming);
				}
			));

			answerList.Add(new ListBinding<UIComponent, Phone.Ans>
			(
				answerList.Children,
				phone.ActiveAnswers,
				delegate(Phone.Ans answer)
				{
					UIComponent button = makeButton(outgoingColor, "\\" + (answer.Text == null ? answer.ID : answer.Text), messageWidth - padding * 4.0f);
					button.Add(new CommandBinding<Point>(button.MouseLeftUp, delegate(Point p)
					{
						phone.Answer(answer);
						
						// Disable the signal tower
						Entity s = signalTower.Value.Target;
						if (s != null && s.Active)
							s.Get<SignalTower>().Initial.Value = null;

						scrollToBottom();
						if (togglePhoneMessage == null && phone.Schedules.Count == 0) // No more messages incoming
							togglePhoneMessage = ((GameMain)main).Menu.ShowMessage(entity, "[{{TogglePhone}}]");
					}));
					return button;
				}
			));

			Action refreshComposeButtonVisibility = delegate()
			{
				bool show = phone.ActiveAnswers.Count > 0 && phone.Schedules.Count == 0;
				answerContainer.Visible.Value &= show;
				composeButton.Visible.Value = show;
				selectedAnswer = 0;
			};
			composeButton.Add(new ListNotifyBinding<Phone.Ans>(refreshComposeButtonVisibility, phone.ActiveAnswers));
			composeButton.Add(new ListNotifyBinding<Phone.Schedule>(refreshComposeButtonVisibility, phone.Schedules));
			refreshComposeButtonVisibility();

			entity.Add(new CommandBinding(phone.MessageReceived, delegate()
			{
				if (phoneActive)
					scrollToBottom();
				else
					showPhone(true);
				
				// Animate the new message
				Container lastMessage = (Container)msgList.Children[msgList.Children.Count - 1].Children[0];
				lastMessage.CheckLayout();
				Vector2 originalSize = lastMessage.Size;
				lastMessage.Size.Value = new Vector2(0, originalSize.Y);
				main.AddComponent(new Animation
				(
					new Animation.Ease(new Animation.Vector2MoveTo(lastMessage.Size, originalSize, 0.5f), Animation.Ease.Type.OutExponential)
				));

				AkSoundEngine.PostEvent("Phone_Play", entity);
				if (togglePhoneMessage == null && phone.Schedules.Count == 0 && phone.ActiveAnswers.Count == 0) // No more messages incoming, and no more answers to give
					togglePhoneMessage = ((GameMain)main).Menu.ShowMessage(entity, "[{{TogglePhone}}]");
			}));

			if (noteActive)
				showNote(true);
			else if (phoneActive)
				showPhone(true);
		}
	}
}