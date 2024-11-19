using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.TwoDimension;

namespace Europe_LemmyProject.MaritimeSystem.ShipManagementUI
{
	public class PrefabTableauTextureProvider : TextureProvider
	{
		private readonly PrefabTableau _prefabTableau;
		private TaleWorlds.Engine.Texture _texture;
		private Texture _providedTexture;

		public string PrefabName
		{
			set
			{
				this._prefabTableau.SetPrefabName(value);
			}
		}

		public bool CurrentlyRotating
		{
			set
			{
				this._prefabTableau.RotateItem(value);
			}
		}

		public float RotateItemVertical
		{
			set
			{
				this._prefabTableau.RotateItemVerticalWithAmount(value);
			}
		}

		public float RotateItemHorizontal
		{
			set
			{
				this._prefabTableau.RotateItemHorizontalWithAmount(value);
			}
		}

		public float InitialTiltRotation
		{
			set
			{
				this._prefabTableau.SetInitialTiltRotation(value);
			}
		}

		public float InitialPanRotation
		{
			set
			{
				this._prefabTableau.SetInitialPanRotation(value);
			}
		}

		public PrefabTableauTextureProvider()
		{
			this._prefabTableau = new PrefabTableau();
		}

		public override void Clear()
		{
			this._prefabTableau.OnFinalize();
			base.Clear();
		}

		private void CheckTexture()
		{
			if (this._texture != this._prefabTableau.Texture)
			{
				this._texture = this._prefabTableau.Texture;
				if (this._texture != null)
				{
					EngineTexture platformTexture = new EngineTexture(this._texture);
					this._providedTexture = new Texture(platformTexture);
					return;
				}
				this._providedTexture = null;
			}
		}

		public override Texture GetTexture(TwoDimensionContext twoDimensionContext, string name)
		{
			this.CheckTexture();
			return this._providedTexture;
		}

		public override void SetTargetSize(int width, int height)
		{
			base.SetTargetSize(width, height);
			this._prefabTableau.SetTargetSize(width, height);
		}

		public override void Tick(float dt)
		{
			base.Tick(dt);
			this.CheckTexture();
			this._prefabTableau.OnTick(dt);
		}
	}
}
