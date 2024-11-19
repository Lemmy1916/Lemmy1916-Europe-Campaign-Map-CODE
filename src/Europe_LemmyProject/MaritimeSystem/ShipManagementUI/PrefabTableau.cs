using System;
using TaleWorlds.Engine;
using TaleWorlds.Engine.Options;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;

namespace Europe_LemmyProject.MaritimeSystem.ShipManagementUI
{
	/// <summary>
	/// Tableau for rendering a prefab. Based on ItemTableau.
	/// </summary>
	public class PrefabTableau
	{
		private Scene _tableauScene;
		private GameEntity _gameEntity;
		private MatrixFrame _itemTableauFrame = MatrixFrame.Identity;
		private bool _isRotating;
		private bool _isTranslating;
		private bool _isRotatingByDefault;
		private bool _initialized;
		private int _tableauSizeX;
		private int _tableauSizeY;
		private Camera _camera;
		private Vec3 _midPoint;
		private float _curZoomSpeed;
		private Vec3 _curCamDisplacement = Vec3.Zero;
		private bool _isEnabled;
		private float _panRotation;
		private float _tiltRotation;
		private bool _hasInitialTiltRotation;
		private float _initialTiltRotation;
		private bool _hasInitialPanRotation;
		private float _initialPanRotation;
		private float RenderScale = 1f;
		private string _prefabName = "";
		private MatrixFrame _initialFrame;
		private bool _lockMouse;

		public Texture Texture { get; private set; }

		private TableauView View
		{
			get
			{
				if (this.Texture != null)
				{
					return this.Texture.TableauView;
				}
				return null;
			}
		}

		private bool _isSizeValid
		{
			get
			{
				return this._tableauSizeX > 0 && this._tableauSizeY > 0;
			}
		}

		public PrefabTableau()
		{
			this.SetEnabled(true);
		}

		public void SetTargetSize(int width, int height)
		{
			bool isSizeValid = this._isSizeValid;
			this._isRotating = false;
			if (width <= 0 || height <= 0)
			{
				this._tableauSizeX = 10;
				this._tableauSizeY = 10;
			}
			else
			{
				this.RenderScale = NativeOptions.GetConfig(NativeOptions.NativeOptionsType.ResolutionScale) / 100f;
				this._tableauSizeX = (int)((float)width * this.RenderScale);
				this._tableauSizeY = (int)((float)height * this.RenderScale);
			}
			//this._cameraRatio = (float)this._tableauSizeX / (float)this._tableauSizeY;
			TableauView view = this.View;
			if (view != null)
			{
				view.SetEnable(false);
			}
			TableauView view2 = this.View;
			if (view2 != null)
			{
				view2.AddClearTask(true);
			}
			Texture texture = this.Texture;
			if (texture != null)
			{
				texture.ReleaseNextFrame();
			}
			if (!isSizeValid && this._isSizeValid)
			{
				this.Recalculate();
			}
			this.Texture = TableauView.AddTableau("PrefabTableau", new RenderTargetComponent.TextureUpdateEventHandler(this.TableauMaterialTabInventoryItemTooltipOnRender), this._tableauScene, this._tableauSizeX, this._tableauSizeY);
		}

		public void OnFinalize()
		{
			TableauView view = this.View;
			if (view != null)
			{
				view.SetEnable(false);
			}
			Camera camera = this._camera;
			if (camera != null)
			{
				camera.ReleaseCameraEntity();
			}
			this._camera = null;
			TableauView view2 = this.View;
			if (view2 != null)
			{
				view2.AddClearTask(false);
			}
			this._tableauScene = null;
			this.Texture = null;
			this._initialized = false;
			if (this._lockMouse)
			{
				this.UpdateMouseLock(true);
			}
		}

		protected void SetEnabled(bool enabled)
		{
			this._isRotatingByDefault = true;
			this._isRotating = false;
			this.ResetCamera();
			this._isEnabled = enabled;
			TableauView view = this.View;
			if (view != null)
			{
				view.SetEnable(this._isEnabled);
			}
		}

		public void SetPrefabName(string stringId)
		{
			this._prefabName = stringId;
			this.Recalculate();
		}

		public void Recalculate()
		{
			if (UiStringHelper.IsStringNoneOrEmptyForUi(this._prefabName) || !this._isSizeValid)
			{
				return;
			}
			this.RefreshItemTableau();
			if (this._gameEntity != null)
			{
				float num = Screen.RealScreenResolutionWidth / (float)this._tableauSizeX;
				float num2 = Screen.RealScreenResolutionHeight / (float)this._tableauSizeY;
                float num3 = (num > num2) ? num : num2;
				float x = _gameEntity.GlobalBoxMax.X - _gameEntity.GlobalBoxMin.X;
				float y = _gameEntity.GlobalBoxMax.Y - _gameEntity.GlobalBoxMin.Y;
				float z	= _gameEntity.GlobalBoxMax.Z - _gameEntity.GlobalBoxMin.Z;
				float scale = num3 / MathF.Max(x,y,z);
				this._itemTableauFrame = MatrixFrame.Identity;
				this._itemTableauFrame.Scale(new Vec3(scale, scale, scale));
				this._gameEntity.SetFrame(ref this._itemTableauFrame);
				if (num3 < 1f)
				{
					Vec3 globalBoxMax = this._gameEntity.GlobalBoxMax;
					Vec3 globalBoxMin = this._gameEntity.GlobalBoxMin;
					this._itemTableauFrame = this._gameEntity.GetFrame();
					float length = this._itemTableauFrame.rotation.f.Length;
					this._itemTableauFrame.rotation.Orthonormalize();
					this._itemTableauFrame.rotation.ApplyScaleLocal(length * num3);
					this._gameEntity.SetFrame(ref this._itemTableauFrame);
					if (globalBoxMax.NearlyEquals(this._gameEntity.GlobalBoxMax, 1E-05f) && globalBoxMin.NearlyEquals(this._gameEntity.GlobalBoxMin, 1E-05f))
					{
						this._gameEntity.SetBoundingboxDirty();
						this._gameEntity.RecomputeBoundingBox();
					}
					this._itemTableauFrame.origin = this._itemTableauFrame.origin + (globalBoxMax + globalBoxMin - this._gameEntity.GlobalBoxMax - this._gameEntity.GlobalBoxMin) * 0.5f;
					this._gameEntity.SetFrame(ref this._itemTableauFrame);
					this._midPoint = (this._gameEntity.GlobalBoxMax + this._gameEntity.GlobalBoxMin) * 0.5f + (globalBoxMax + globalBoxMin - this._gameEntity.GlobalBoxMax - this._gameEntity.GlobalBoxMin) * 0.5f;
				}
				else
				{
					this._midPoint = (this._gameEntity.GlobalBoxMax + this._gameEntity.GlobalBoxMin) * 0.5f;
				}
				this.ResetCamera();
			}
			this._isRotatingByDefault = true;
			this._isRotating = false;
		}

		public void Initialize()
		{
			this._isRotatingByDefault = true;
			this._isRotating = false;
			this._isTranslating = false;
			this._tableauScene = Scene.CreateNewScene(true, false, DecalAtlasGroup.All, "mono_renderscene");
			this._tableauScene.SetName("PrefabTableau");
			this._tableauScene.DisableStaticShadows(true);
			this._tableauScene.SetAtmosphereWithName("character_menu_a");
			Vec3 vec = new Vec3(1f, -1f, -1f, -1f);
			this._tableauScene.SetSunDirection(ref vec);
			this.ResetCamera();
			this._initialized = true;
		}

		private void TranslateCamera(bool value)
		{
			this.TranslateCameraAux(value);
		}

		private void TranslateCameraAux(bool value)
		{
			this._isRotatingByDefault = (!value && this._isRotatingByDefault);
			this._isTranslating = value;
			this.UpdateMouseLock(false);
		}

		private void ResetCamera()
		{
			this._curCamDisplacement = Vec3.Zero;
			this._curZoomSpeed = 0f;
			if (this._camera != null)
			{
				this._camera.Frame = MatrixFrame.Identity;
				this.SetCamFovHorizontal(1f);
				this.MakeCameraLookMidPoint();
			}
		}

		public void RotateItem(bool value)
		{
			this._isRotatingByDefault = (!value && this._isRotatingByDefault);
			this._isRotating = value;
			this.UpdateMouseLock(false);
		}

		public void RotateItemVerticalWithAmount(float value)
		{
			this.UpdateRotation(0f, value / -2f);
		}

		public void RotateItemHorizontalWithAmount(float value)
		{
			this.UpdateRotation(value / 2f, 0f);
		}

		public void OnTick(float dt)
		{
			float num = Input.MouseMoveX + Input.GetKeyState(InputKey.ControllerLStick).X * 1000f * dt;
			float num2 = Input.MouseMoveY + Input.GetKeyState(InputKey.ControllerLStick).Y * -1000f * dt;
			if (this._isEnabled && (this._isRotating || this._isTranslating) && (!num.ApproximatelyEqualsTo(0f, 1E-05f) || !num2.ApproximatelyEqualsTo(0f, 1E-05f)))
			{
				if (this._isRotating)
				{
					this.UpdateRotation(num, num2);
				}
				if (this._isTranslating)
				{
					this.UpdatePosition(num, num2);
				}
			}
			this.TickCameraZoom(dt);
		}

		private void UpdatePosition(float mouseMoveX, float mouseMoveY)
		{
			if (this._initialized)
			{
				Vec3 vec = new Vec3(mouseMoveX / (float)(-(float)this._tableauSizeX), mouseMoveY / (float)this._tableauSizeY, 0f, -1f);
				vec *= 2.2f * this._camera.HorizontalFov;
				this._curCamDisplacement += vec;
				this.MakeCameraLookMidPoint();
			}
		}

		private void UpdateRotation(float mouseMoveX, float mouseMoveY)
		{
			if (this._initialized)
			{
				this._panRotation += mouseMoveX * 0.004363323f;
				this._tiltRotation += mouseMoveY * 0.004363323f;
				this._tiltRotation = MathF.Clamp(this._tiltRotation, -2.984513f, -0.15707964f);
				MatrixFrame m = this._gameEntity.GetFrame();
				Vec3 vec = (this._gameEntity.GetBoundingBoxMax() + this._gameEntity.GetBoundingBoxMin()) * 0.5f;
				MatrixFrame identity = MatrixFrame.Identity;
				//identity.origin = vec;
				MatrixFrame identity2 = MatrixFrame.Identity;
				//identity2.origin = -vec;
				//m *= identity;
				m.rotation = Mat3.Identity;
				m.rotation.ApplyScaleLocal(this._initialFrame.rotation.GetScaleVector());
				m.rotation.RotateAboutSide(this._tiltRotation);
				m.rotation.RotateAboutUp(this._panRotation);
				//m *= identity2;
				m.Scale((0.9f/vec.Length)*Vec3.One);
				m.origin = new Vec3(0f, -0.8f, m.origin.Z);
				this._gameEntity.SetFrame(ref m);
			}
		}

		public void SetInitialTiltRotation(float amount)
		{
			this._hasInitialTiltRotation = true;
			this._initialTiltRotation = amount;
		}

		public void SetInitialPanRotation(float amount)
		{
			this._hasInitialPanRotation = true;
			this._initialPanRotation = amount;
		}

		public void Zoom(double value)
		{
			this._curZoomSpeed -= (float)(value / 1000.0);
		}

		private void RefreshItemTableau()
		{
			if (!this._initialized)
			{
				this.Initialize();
			}
			if (this._gameEntity != null)
			{
				this._gameEntity.Remove(102);
				this._gameEntity = null;
			}
				
			if (this._gameEntity == null)
			{
				MatrixFrame itemFrameForItemTooltip = MatrixFrame.Identity;
				itemFrameForItemTooltip.origin.z = itemFrameForItemTooltip.origin.z + 2.5f;
				if (GameEntity.PrefabExists(_prefabName))
				{
					this._gameEntity = GameEntity.Instantiate(this._tableauScene, _prefabName, itemFrameForItemTooltip);
					float x = _gameEntity.GetBoundingBoxMax().Z - _gameEntity.GetBoundingBoxMin().Z;
					float y = _gameEntity.GetBoundingBoxMax().Y - _gameEntity.GetBoundingBoxMin().Y;
					float z = _gameEntity.GetBoundingBoxMax().X - _gameEntity.GetBoundingBoxMin().X;

					float maxLen = MathF.Max(x, y, z);

					//itemFrameForItemTooltip.Scale((1f/(maxLen * 1.1f)) * Vec3.One);
					//_gameEntity.SetFrame(ref itemFrameForItemTooltip);
				}
				else
				{
					MBDebug.ShowWarning("[DEBUG]Item with " + this._prefabName + " string id cannot be found");
				}
			}
			TableauView view = this.View;
			if (view != null)
			{
				float radius = (this._gameEntity.GetBoundingBoxMax() - this._gameEntity.GetBoundingBoxMin()).Length * 2f;
				Vec3 origin = this._gameEntity.GetGlobalFrame().origin;
				view.SetFocusedShadowmap(true, ref origin, radius);
			}
			if (this._gameEntity != null)
			{
				this._initialFrame = this._gameEntity.GetFrame();
				Vec3 eulerAngles = this._initialFrame.rotation.GetEulerAngles();
				this._panRotation = eulerAngles.x;
				this._tiltRotation = eulerAngles.z;
				if (this._hasInitialPanRotation)
				{
					this._panRotation = this._initialPanRotation;
				}

				if (this._hasInitialTiltRotation)
				{
					this._tiltRotation = this._initialTiltRotation;
					return;
				}

				this._tiltRotation = -1.5707964f;
			}
		}

		private void TableauMaterialTabInventoryItemTooltipOnRender(Texture sender, EventArgs e)
		{
			if (this._initialized)
			{
				TableauView tableauView = this.View;
				if (tableauView == null)
				{
					tableauView = sender.TableauView;
					tableauView.SetEnable(this._isEnabled);
				}
				if (this._gameEntity == null)
				{
					tableauView.SetContinuousRendering(false);
					tableauView.SetDeleteAfterRendering(true);
					return;
				}
				tableauView.SetRenderWithPostfx(true);
				tableauView.SetClearColor(0U);
				tableauView.SetScene(this._tableauScene);
				if (this._camera == null)
				{
					this._camera = Camera.CreateCamera();
					this._camera.SetViewVolume(true, -0.5f, 0.5f, -0.5f, 0.5f, 0.01f, 100f);
					this.ResetCamera();
					tableauView.SetSceneUsesSkybox(false);
				}
				tableauView.SetCamera(this._camera);
				if (this._isRotatingByDefault)
				{
					this.UpdateRotation(1f, 0f);
				}
				tableauView.SetDeleteAfterRendering(false);
				tableauView.SetContinuousRendering(true);
			}
		}

		// Token: 0x06000150 RID: 336 RVA: 0x0000B664 File Offset: 0x00009864
		private void MakeCameraLookMidPoint()
		{
			Vec3 v = this._camera.Frame.rotation.TransformToParent(this._curCamDisplacement);
			Vec3 v2 = this._midPoint + v;
			float f = this._midPoint.Length * 0.5263158f;
			Vec3 position = v2 - this._camera.Direction * f;
			this._camera.Position = position;
		}

		// Token: 0x06000151 RID: 337 RVA: 0x0000B6D1 File Offset: 0x000098D1
		private void SetCamFovHorizontal(float camFov)
		{
			this._camera.SetFovHorizontal(camFov, 1f, 0.1f, 50f);
		}

		// Token: 0x06000152 RID: 338 RVA: 0x0000B6EE File Offset: 0x000098EE
		private void UpdateMouseLock(bool forceUnlock = false)
		{
			this._lockMouse = ((this._isRotating || this._isTranslating) && !forceUnlock);
			MouseManager.LockCursorAtCurrentPosition(this._lockMouse);
			MouseManager.ShowCursor(!this._lockMouse);
		}

		// Token: 0x06000153 RID: 339 RVA: 0x0000B728 File Offset: 0x00009928
		private void TickCameraZoom(float dt)
		{
			if (this._camera != null)
			{
				float num = this._camera.HorizontalFov;
				num += this._curZoomSpeed;
				num = MBMath.ClampFloat(num, 0.1f, 2f);
				this.SetCamFovHorizontal(num);
				if (dt > 0f)
				{
					this._curZoomSpeed = MBMath.Lerp(this._curZoomSpeed, 0f, MBMath.ClampFloat(dt * 25.9f, 0f, 1f), 1E-05f);
				}
			}
		}
	}

	/// <summary>
	/// Widget for loading a PrefabTableau. Based on ItemTableauWidget
	/// </summary>
	public class PrefabTableauWidget : TextureWidget
	{

		[Editor(false)]
		public string PrefabName
		{
			get
			{
				return this._prefabName;
			}
			set
			{
				this._prefabName = value;
				base.OnPropertyChanged(value, "PrefabName");
				if (value != null)
				{
					base.SetTextureProviderProperty("PrefabName", value);
				}
			}
		}

		[Editor(false)]
		public float InitialTiltRotation
		{
			get
			{
				return this._initialTiltRotation;
			}
			set
			{
				if (value != this._initialTiltRotation)
				{
					this._initialTiltRotation = value;
					base.OnPropertyChanged(value, "InitialTiltRotation");
					base.SetTextureProviderProperty("InitialTiltRotation", value);
				}
			}
		}

		[Editor(false)]
		public float InitialPanRotation
		{
			get
			{
				return this._initialPanRotation;
			}
			set
			{
				if (value != this._initialPanRotation)
				{
					this._initialPanRotation = value;
					base.OnPropertyChanged(value, "InitialPanRotation");
					base.SetTextureProviderProperty("InitialPanRotation", value);
				}
			}
		}

		public PrefabTableauWidget(UIContext context) : base(context)
		{
			base.TextureProviderName = "PrefabTableauTextureProvider";
		}

		protected override void OnMousePressed()
		{
			base.SetTextureProviderProperty("CurrentlyRotating", true);
		}

		protected override void OnRightStickMovement()
		{
			base.OnRightStickMovement();
			base.SetTextureProviderProperty("RotateItemVertical", base.EventManager.RightStickVerticalScrollAmount);
			base.SetTextureProviderProperty("RotateItemHorizontal", base.EventManager.RightStickHorizontalScrollAmount);
		}

		protected override void OnMouseReleased()
		{
			base.SetTextureProviderProperty("CurrentlyRotating", false);
		}

		protected override bool OnPreviewRightStickMovement()
		{
			return false;
		}

		private string _prefabName;
		private float _initialTiltRotation;
		private float _initialPanRotation;
	}
}
