using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace StardewMCPBridge
{
    /// <summary>
    /// An NPC wrapper that renders as a Farmer instead of using a static sprite.
    /// The NPC handles pathfinding and position; the paired BotFarmer handles rendering.
    /// </summary>
    public class CompanionNpc : NPC
    {
        public BotFarmer FarmerBody;
        private Vector2 lastPosition;
        private int animFrameCounter = 0;

        public CompanionNpc(AnimatedSprite sprite, Vector2 position, string locationName,
            int facingDir, string name, Texture2D portrait, bool eventActor)
            : base(sprite, position, locationName, facingDir, name, portrait, eventActor)
        {
            this.lastPosition = position;
        }

        public override void draw(SpriteBatch b, float alpha = 1f)
        {
            if (this.FarmerBody == null)
            {
                base.draw(b, alpha);
                return;
            }

            // Sync farmer's position and facing from NPC
            this.FarmerBody.Position = this.Position;
            this.FarmerBody.FacingDirection = this.FacingDirection;
            this.FarmerBody.currentLocation = this.currentLocation;

            // Detect movement to drive walking animation
            Vector2 delta = this.Position - this.lastPosition;
            bool isMoving = delta.LengthSquared() > 0.01f;
            this.lastPosition = this.Position;

            if (isMoving)
            {
                // Advance walk cycle every ~6 frames
                this.animFrameCounter++;
                if (this.animFrameCounter >= 6)
                {
                    this.animFrameCounter = 0;
                    // Walk cycle has 4 frames per direction: idle, step-right, idle, step-left
                    int baseFrame = this.FacingDirection * 4;
                    int currentFrame = this.FarmerBody.FarmerSprite.CurrentFrame;
                    int step = currentFrame - baseFrame;
                    if (step < 0 || step > 3) step = 0;
                    step = (step + 1) % 4;
                    this.FarmerBody.FarmerSprite.setCurrentFrame(baseFrame + step);
                }
            }
            else
            {
                // When idle, face south (towards the camera)
                this.FacingDirection = 2;
                this.FarmerBody.FacingDirection = 2;
                this.FarmerBody.FarmerSprite.setCurrentFrame(0);
                this.animFrameCounter = 0;
            }

            try
            {
                this.FarmerBody.draw(b);
            }
            catch
            {
                base.draw(b, alpha);
            }
        }
    }
}
