

using Myd.Common;
using Myd.Platform;
using Myd.Platform.Core;
using UnityEngine;

namespace Myd.Platform
{
    /// <summary>
    /// 玩家类：包含
    /// 1、玩家显示器
    /// 2、玩家控制器（核心控制器）
    /// 并允许两者在内部进行交互
    /// </summary>
    public class Player
    {
        private PlayerRenderer playerRenderer;
        private PlayerController playerController;

        private IGameContext gameContext;

        public Vector2 Position => this.playerController != null ? this.playerController.Position : Vector2.zero;
        public int Facing => this.playerController != null ? (int)this.playerController.Facing : 1;
        public bool OnGround => this.playerController != null && this.playerController.OnGround;
        public bool CanAttack => this.playerController != null && this.playerController.CanAttack;

        public void StaggerFromClash()
        {
            if (this.playerController != null) this.playerController.StaggerFromClash();
        }

        public void MoveForAttack(float distance)
        {
            if (this.playerController == null || !this.playerController.CanMoveForAttack)
            {
                return;
            }
            this.playerController.AdjustPosition(new Vector2(distance, 0f));
            Render();
        }

        public void SetDamageFlash(bool faded)
        {
            if (this.playerRenderer == null)
            {
                return;
            }

            SetRendererAlpha(this.playerRenderer.spriteRenderer, faded);
            SetRendererAlpha(this.playerRenderer.hairSprite01, faded);
            SetRendererAlpha(this.playerRenderer.hairSprite02, faded);
        }

        private static void SetRendererAlpha(SpriteRenderer renderer, bool faded)
        {
            if (renderer == null)
            {
                return;
            }
            Color color = renderer.color;
            color.a = faded ? 0.3f : 1f;
            renderer.color = color;
        }

        public Player(IGameContext gameContext)
        {
            this.gameContext = gameContext;
        }

        //加载玩家实体
        public void Reload(Bounds bounds, Vector2 startPosition)
        {
            if (this.playerRenderer != null)
            {
                Object.Destroy(this.playerRenderer.gameObject);
            }

            this.playerRenderer = Object.Instantiate(Resources.Load<PlayerRenderer>("PlayerRenderer"));
            //this.playerRenderer = AssetHelper.Create<PlayerRenderer>("Assets/ProPlatformer/_Prefabs/PlayerRenderer.prefab");
            this.playerRenderer.Reload();
            //初始化
            this.playerController = new PlayerController(playerRenderer, gameContext.EffectControl);
            this.playerController.Init(bounds, startPosition);

            PlayerParams playerParams = Resources.Load<PlayerParams>("PlayerParam");
            //PlayerParams playerParams = AssetHelper.LoadObject<PlayerParams>("Assets/ProPlatformer/PlayerParam.asset");
            playerParams.SetReloadCallback(() => this.playerController.RefreshAbility());
            playerParams.ReloadParams();
        }

        public bool TouchesSpike(SpriteRenderer[] spikes)
        {
            if (this.playerRenderer == null || this.playerRenderer.spriteRenderer == null || spikes == null)
            {
                return false;
            }

            Bounds playerBounds = this.playerRenderer.spriteRenderer.bounds;
            foreach (SpriteRenderer spike in spikes)
            {
                if (spike != null && playerBounds.Intersects(spike.bounds))
                {
                    return true;
                }
            }

            return false;
        }

        public bool Overlaps(Bounds otherBounds)
        {
            return this.playerRenderer != null &&
                   this.playerRenderer.spriteRenderer != null &&
                   this.playerRenderer.spriteRenderer.bounds.Intersects(otherBounds);
        }

        public void Update(float deltaTime, EnemyBlock[] enemies, ArenaBoss boss)
        {
            playerController.SetSolidEnemies(enemies);
            playerController.SetSolidBoss(boss);
            playerController.Update(deltaTime);
            Render();
        }

        private void Render()
        {
            playerRenderer.Render(Time.deltaTime);

            Vector2 scale = playerRenderer.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (int)playerController.Facing;
            playerRenderer.transform.localScale = scale;
            playerRenderer.transform.position = playerController.Position;

            //if (!lastFrameOnGround && this.playerController.OnGround)
            //{
            //    this.playerRenderer.PlayMoveEffect(true, this.playerController.GroundColor);
            //}
            //else if (lastFrameOnGround && !this.playerController.OnGround)
            //{
            //    this.playerRenderer.PlayMoveEffect(false, this.playerController.GroundColor);
            //}
            //this.playerRenderer.UpdateMoveEffect();

            this.lastFrameOnGround = this.playerController.OnGround;
        }

        private bool lastFrameOnGround;

        public Vector2 GetCameraPosition()
        {
            if (this.playerController == null)
            {
                return Vector3.zero;
            }
            return playerController.GetCameraPosition();
        }
    }

}
