using UnityEngine;
using UnityEngine.SceneManagement;

namespace Myd.Platform
{
    // A self-contained encounter built in an additive runtime scene.
    public sealed class ArenaBoss
    {
        public const float ArenaCenter = 1000f;
        public const float FloorTop = -0.2f;
        public static readonly Vector2 PlayerSpawn = new Vector2(ArenaCenter - 6f, 0.62f);
        public static readonly Bounds ArenaBounds = new Bounds(
            new Vector3(ArenaCenter, 4f, 0f), new Vector3(40f, 22f, 1f));
        public const int MaxHealth = 160;

        private enum Phase { Wait, Telegraph, Strike, Recovery, Defeated }
        private readonly GameObject root;
        private readonly GameObject boss;
        private readonly SpriteRenderer body;
        private readonly SpriteRenderer warning;
        private readonly Texture2D texture;
        private readonly Sprite sprite;
        private readonly Vector3 bossStart = new Vector3(ArenaCenter + 4f, 0.8f, 0f);
        private Phase phase;
        private float timer;
        private int attackIndex;
        private int direction;
        private bool attackHit;
        private float flashTimer;
        private float victoryTimer;
        private Bounds strikeBounds;

        public int Health { get; private set; }
        public bool Defeated => this.phase == Phase.Defeated;
        public bool VictoryComplete => Defeated && this.victoryTimer <= 0f;
        public Bounds BodyBounds => this.body.bounds;

        public ArenaBoss(Scene scene)
        {
            this.texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            this.texture.name = "ArenaPixel";
            this.texture.SetPixel(0, 0, Color.white);
            this.texture.Apply();
            this.sprite = Sprite.Create(this.texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            this.root = new GameObject("Boss Arena");
            SceneManager.MoveGameObjectToScene(this.root, scene);

            MakeBlock("Background", new Vector2(ArenaCenter, 4f), new Vector2(24f, 12f),
                new Color(0.075f, 0.055f, 0.12f), false, -10);
            MakeBlock("Arena Floor", new Vector2(ArenaCenter, FloorTop - 1f), new Vector2(22f, 2f),
                new Color(0.28f, 0.22f, 0.34f), true, 0);
            MakeBlock("Left Wall", new Vector2(ArenaCenter - 11f, 4f), new Vector2(1f, 10f),
                new Color(0.22f, 0.17f, 0.28f), true, 0);
            MakeBlock("Right Wall", new Vector2(ArenaCenter + 11f, 4f), new Vector2(1f, 10f),
                new Color(0.22f, 0.17f, 0.28f), true, 0);

            this.body = MakeBlock("Boss", this.bossStart, new Vector2(2f, 2f),
                new Color(0.44f, 0.2f, 0.59f), false, 4);
            this.boss = this.body.gameObject;
            MakeBossDetail("Left eye", new Vector2(-0.23f, 0.13f), new Vector2(0.17f, 0.13f), Color.white);
            MakeBossDetail("Right eye", new Vector2(0.23f, 0.13f), new Vector2(0.17f, 0.13f), Color.white);
            MakeBossDetail("Mouth", new Vector2(0f, -0.21f), new Vector2(0.3f, 0.05f), new Color(0.08f, 0.03f, 0.1f));
            this.warning = MakeBlock("Attack warning", Vector2.zero, Vector2.one,
                new Color(1f, 0.2f, 0.13f, 0.42f), false, 5);
            this.warning.enabled = false;
            Reset();
        }

        private SpriteRenderer MakeBlock(string name, Vector2 position, Vector2 size, Color color, bool solid, int order)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(this.root.transform, false);
            obj.transform.position = position;
            obj.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = this.sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            if (solid)
            {
                obj.layer = LayerMask.NameToLayer("Ground");
                obj.AddComponent<BoxCollider2D>();
                obj.AddComponent<Ground>().GroundColor = color;
            }
            return renderer;
        }

        private void MakeBossDetail(string name, Vector2 localPosition, Vector2 localSize, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(this.boss.transform, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localScale = new Vector3(localSize.x, localSize.y, 1f);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = this.sprite;
            renderer.color = color;
            renderer.sortingOrder = 5;
        }

        public void Reset()
        {
            this.Health = MaxHealth;
            this.phase = Phase.Wait;
            this.timer = 0.8f;
            this.attackIndex = 0;
            this.victoryTimer = 0f;
            this.flashTimer = 0f;
            this.boss.transform.position = this.bossStart;
            this.body.color = new Color(0.44f, 0.2f, 0.59f);
            this.warning.enabled = false;
        }

        public void TakeDamage(int damage)
        {
            if (Defeated || damage <= 0) return;
            this.Health = Mathf.Max(0, this.Health - damage);
            this.flashTimer = 0.13f;
            if (this.Health == 0)
            {
                this.phase = Phase.Defeated;
                this.victoryTimer = 2.5f;
                this.warning.enabled = false;
                this.body.color = new Color(0.25f, 0.23f, 0.3f);
            }
        }

        // Returns true only once per attack, when its active area hits the player.
        public bool Tick(float deltaTime, Vector2 playerPosition)
        {
            if (Defeated)
            {
                this.victoryTimer -= deltaTime;
                return false;
            }
            this.flashTimer = Mathf.Max(0f, this.flashTimer - deltaTime);
            this.body.color = this.flashTimer > 0f ? Color.white : new Color(0.44f, 0.2f, 0.59f);
            this.timer -= deltaTime;

            if (this.phase == Phase.Wait && this.timer <= 0f)
            {
                this.direction = playerPosition.x < this.boss.transform.position.x ? -1 : 1;
                this.phase = Phase.Telegraph;
                this.timer = this.attackIndex % 2 == 0 ? 0.8f : 1f;
                this.attackHit = false;
                float x = this.boss.transform.position.x;
                if (this.attackIndex % 2 == 0)
                    this.strikeBounds = new Bounds(new Vector3(x + direction * 2f, 0.75f, 0f), new Vector3(3.4f, 2.1f, 1f));
                else
                    this.strikeBounds = new Bounds(new Vector3(Mathf.Clamp(playerPosition.x, ArenaCenter - 9.5f, ArenaCenter + 9.5f), 0.35f, 0f), new Vector3(3.2f, 1.2f, 1f));
                this.warning.transform.position = this.strikeBounds.center;
                this.warning.transform.localScale = this.strikeBounds.size;
                this.warning.color = new Color(1f, 0.2f, 0.13f, 0.42f);
                this.warning.enabled = true;
            }
            else if (this.phase == Phase.Telegraph && this.timer <= 0f)
            {
                this.phase = Phase.Strike;
                this.timer = 0.23f;
                this.warning.color = new Color(1f, 0.77f, 0.32f, 0.9f);
            }
            else if (this.phase == Phase.Strike)
            {
                if (!this.attackHit)
                {
                    Bounds playerBounds = new Bounds(playerPosition + Vector2.down * 0.25f, new Vector3(0.8f, 1.1f, 1f));
                    if (this.strikeBounds.Intersects(playerBounds))
                    {
                        this.attackHit = true;
                        return true;
                    }
                }
                if (this.timer <= 0f)
                {
                    this.warning.enabled = false;
                    this.phase = Phase.Recovery;
                    this.timer = 0.7f;
                    this.attackIndex++;
                }
            }
            else if (this.phase == Phase.Recovery && this.timer <= 0f)
            {
                this.phase = Phase.Wait;
                this.timer = 0.45f;
            }

            if (this.phase == Phase.Wait && Mathf.Abs(playerPosition.x - this.boss.transform.position.x) > 2.5f)
            {
                Vector3 position = this.boss.transform.position;
                position.x = Mathf.MoveTowards(position.x, playerPosition.x, 1.4f * deltaTime);
                position.x = Mathf.Clamp(position.x, ArenaCenter - 9.4f, ArenaCenter + 9.4f);
                this.boss.transform.position = position;
            }
            return false;
        }

        public void Dispose()
        {
            Object.Destroy(this.root);
            Object.Destroy(this.sprite);
            Object.Destroy(this.texture);
        }
    }
}
