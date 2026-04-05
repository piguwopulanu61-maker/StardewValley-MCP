using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace StardewMCPBridge
{
    /// <summary>
    /// A Farmer subclass that serves as an AI-controlled farmhand.
    /// Renders as a full farmer with randomized appearance (Player 2-style).
    /// </summary>
    public class BotFarmer : Farmer
    {
        public bool IsBot { get; } = true;

        private static readonly Random rng = new Random();

        /// <summary>
        /// Initialize the farmer with sprite, appearance, and basic state.
        /// Call this after construction and before use.
        /// </summary>
        public void Initialize(string name, bool isMale = true)
        {
            this.FarmerSprite = new FarmerSprite(@"Characters\Farmer\farmer_base");

            this.Gender = isMale ? Gender.Male : Gender.Female;
            this.skin.Set(rng.Next(0, 6));
            this.hair.Set(rng.Next(0, 16));

            this.hairstyleColor.Set(RandomColor());
            this.pantsColor.Set(RandomColor());
            this.newEyeColor.Set(RandomColor());

            this.shirtItem.Value = null;
            this.shirt.Set(rng.Next(1000, 1112).ToString());

            this.FacingDirection = 2;
        }

        /// <summary>Set specific appearance values. Null means keep current.</summary>
        public void SetAppearance(int? skin, int? hair, bool? isMale, int? shirt,
            int? hairColorR, int? hairColorG, int? hairColorB,
            int? pantsColorR, int? pantsColorG, int? pantsColorB,
            int? eyeColorR, int? eyeColorG, int? eyeColorB)
        {
            if (isMale.HasValue) this.Gender = isMale.Value ? Gender.Male : Gender.Female;
            if (skin.HasValue) this.skin.Value = skin.Value;
            if (hair.HasValue) this.hair.Value = hair.Value;
            if (shirt.HasValue) { this.shirtItem.Value = null; this.shirt.Value = shirt.Value.ToString(); }
            if (hairColorR.HasValue && hairColorG.HasValue && hairColorB.HasValue)
                this.hairstyleColor.Value = new Color(hairColorR.Value, hairColorG.Value, hairColorB.Value);
            if (pantsColorR.HasValue && pantsColorG.HasValue && pantsColorB.HasValue)
                this.pantsColor.Value = new Color(pantsColorR.Value, pantsColorG.Value, pantsColorB.Value);
            if (eyeColorR.HasValue && eyeColorG.HasValue && eyeColorB.HasValue)
                this.newEyeColor.Value = new Color(eyeColorR.Value, eyeColorG.Value, eyeColorB.Value);

            // Force the renderer to rebuild the recolored sprite sheet
            this.FarmerRenderer?.MarkSpriteDirty();
        }

        private static Color RandomColor()
        {
            return new Color(rng.Next(50, 256), rng.Next(50, 256), rng.Next(50, 256));
        }

        public override void SetMovingUp(bool b)
        {
            if (!b) Halt();
            else moveUp = true;
        }

        public override void SetMovingRight(bool b)
        {
            if (!b) Halt();
            else moveRight = true;
        }

        public override void SetMovingDown(bool b)
        {
            if (!b) Halt();
            else moveDown = true;
        }

        public override void SetMovingLeft(bool b)
        {
            if (!b) Halt();
            else moveLeft = true;
        }

        public new void tryToMoveInDirection(int direction, bool isFarmer, int damagesFarmer, bool glider)
        {
            bool canPass = currentLocation.isTilePassable(nextPosition(direction), Game1.viewport);
            if (canPass)
            {
                switch (direction)
                {
                    case 0: position.Y -= speed + addedSpeed; break;
                    case 1: position.X += speed + addedSpeed; break;
                    case 2: position.Y += speed + addedSpeed; break;
                    case 3: position.X -= speed + addedSpeed; break;
                }
            }
        }

        public void FaceToward(Vector2 targetTile)
        {
            Vector2 diff = targetTile * 64f - this.Position;
            if (Math.Abs(diff.X) > Math.Abs(diff.Y))
                this.FacingDirection = diff.X > 0 ? 1 : 3;
            else
                this.FacingDirection = diff.Y > 0 ? 2 : 0;
        }

        public void WakeUp()
        {
            this.isInBed.Value = false;
            this.sleptInTemporaryBed.Value = false;
            this.Stamina = this.MaxStamina;
            this.health = this.maxHealth;
        }

        public void SignalSleepReady()
        {
            this.isInBed.Value = true;
        }
    }
}
