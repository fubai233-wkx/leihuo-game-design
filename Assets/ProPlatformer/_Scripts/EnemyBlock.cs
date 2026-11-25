using UnityEngine;

namespace Myd.Platform
{
    /// <summary>
    /// An enemy placed directly in the scene.
    /// It uses the same bounds-based collision as the custom player controller.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class EnemyBlock : MonoBehaviour
    {
        private const int SpriteSize = 16;
        [SerializeField, Min(0f)] private float patrolDistance = 1.2f;
        [SerializeField, Min(0f)] private float patrolSpeed = 1.4f;
        [SerializeField] private bool ranged;
        [SerializeField, Min(1f)] private float rangedSight = 10f;
        [SerializeField, Min(0.1f)] private float aimDuration = 0.9f;
        [SerializeField, Min(0.1f)] private float shotCooldown = 1.2f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 15f;
        [SerializeField, Min(0.05f)] private float projectileRadius = 0.18f;
        [SerializeField, Min(0f)] private float targetAcquisitionTime = 0.35f;

        private enum RangedState { Idle, Aiming, Flying, Cooldown }
        private RangedState rangedState;
        private float rangedTimer;
        private Vector2 shotOrigin;
        private Vector2 shotDirection;
        private Vector2 projectilePosition;
        private float projectileTravel;
        private float shotDistance;
        private float acquisitionTimer;
        private Vector2 previousPlayerPosition;
        private Vector2 estimatedPlayerVelocity;
        private bool hasPreviousPlayerPosition;
        private SpriteRenderer projectileRenderer;
        private Sprite projectileSprite;
        private Texture2D projectileTexture;
        private LineRenderer sightLine;
        private Material sightMaterial;

        private SpriteRenderer spriteRenderer;
        private Sprite originalSprite;
        private Vector3 startPosition;
        private int patrolDirection = 1;
        private enum ActionState { Moving, Windup, Attack, Recovery, Stagger }
        private ActionState actionState;
        private float actionTimer;
        private int attackDirection;
        private Vector2 attackAim;
        private Vector3 originalScale;
        private Quaternion originalRotation;
        private float staggerDuration;
        private int staggerTiltDirection = 1;
        private Color originalColor;
        [SerializeField, Min(0f)] private float detectionRange = 12f;
        [SerializeField, Min(0f)] private float chaseSpeed = 3.8f;
        [SerializeField, Min(0.05f)] private float windupTime = 0.22f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.35f;
        private const float AttackTime = 0.2f;
        private const float RecoveryTime = 0.4f;
        private const float HitStaggerTime = 0.38f;
        private const float HeavyStaggerTime = 0.65f;
        private Sprite weaponSprite;
        private SpriteRenderer weaponRenderer;
        private LineRenderer attackArc;
        private Material arcMaterial;
        private Texture2D spriteTexture;
        private Sprite enemySprite;
        private int groundMask;
        [SerializeField, Min(1)] private int meleeMaxHealth = 60;
        [SerializeField, Min(1)] private int rangedMaxHealth = 20;
        private int MaxHealth => this.ranged ? this.rangedMaxHealth : this.meleeMaxHealth;
        private const float DeathDisplayTime = 0.45f;
        private const float HealthBarWidth = 1.08f;
        private int currentHealth;
        private float deathTimer;
        private bool isDead;
        private GameObject healthBarRoot;
        private SpriteRenderer healthBarFill;
        private Sprite healthBarSprite;
        private Texture2D healthBarTexture;

        public bool IsAlive => !this.isDead;
        public bool IsRanged => this.ranged;
        public int CurrentHealth => this.currentHealth;

        public Bounds Bounds => this.spriteRenderer != null
            ? this.spriteRenderer.bounds
            : new Bounds(this.transform.position, Vector3.zero);

        private void Awake()
        {
            this.spriteRenderer = GetComponent<SpriteRenderer>();
            this.originalSprite = this.spriteRenderer.sprite;
            this.groundMask = LayerMask.GetMask("Ground");
            this.startPosition = this.transform.position;
            this.originalScale = this.transform.localScale;
            this.originalRotation = this.transform.localRotation;
            this.originalColor = this.spriteRenderer.color;
            this.currentHealth = MaxHealth;
            CreateHealthBar();
            if (this.ranged) CreateRangedVisuals();
            else CreateWeapon();
        }

        public bool Tick(float deltaTime, Vector2 playerPosition, bool playerOnGround)
        {
            if (this.isDead)
            {
                this.deathTimer -= deltaTime;
                if (this.deathTimer <= 0f)
                {
                    this.gameObject.SetActive(false);
                }
                return false;
            }

            if (this.ranged)
            {
                return TickRanged(deltaTime, playerPosition);
            }

            if (this.actionState != ActionState.Moving)
            {
                this.actionTimer -= deltaTime;
                if (this.actionState == ActionState.Stagger)
                {
                    float progress = 1f - Mathf.Clamp01(this.actionTimer / this.staggerDuration);
                    float tilt = Mathf.Sin(progress * Mathf.PI) * 28f * this.staggerTiltDirection;
                    this.transform.localRotation = this.originalRotation * Quaternion.Euler(0f, 0f, tilt);
                    this.transform.localScale = Vector3.Scale(this.originalScale, new Vector3(0.8f, 1.2f, 1f));
                    this.spriteRenderer.color = Mathf.FloorToInt(progress * 6f) % 2 == 0
                        ? Color.white : new Color(1f, 0.35f, 0.35f, 1f);
                    if (this.actionTimer <= 0f)
                    {
                        this.spriteRenderer.color = this.originalColor;
                        this.transform.localScale = this.originalScale;
                        this.transform.localRotation = this.originalRotation;
                        this.actionState = ActionState.Moving;
                    }
                    return false;
                }
                if (this.actionState == ActionState.Windup)
                {
                    UpdateWeapon(-55f, false);
                    this.transform.localScale = Vector3.Scale(this.originalScale, new Vector3(1.12f, 0.8f, 1f));
                    this.spriteRenderer.color = Color.Lerp(this.originalColor, Color.yellow, 0.5f);
                    if (this.actionTimer <= 0f)
                    {
                        this.actionState = ActionState.Attack;
                        this.actionTimer = AttackTime;
                        this.transform.localScale = Vector3.Scale(this.originalScale, new Vector3(1.25f, 0.9f, 1f));
                        this.spriteRenderer.color = Color.white;
                        UpdateWeapon(60f, true);
                        Vector2 center = this.transform.position;
                        Bounds playerBounds = new Bounds(playerPosition + Vector2.down * 0.25f,
                            new Vector3(0.8f, 1.1f, 1f));
                        Vector2 delta = (Vector2)playerBounds.ClosestPoint(center) - center;
                        return delta.sqrMagnitude <= this.attackRange * this.attackRange
                            && (delta.sqrMagnitude < 0.0001f || Vector2.Dot(delta.normalized, this.attackAim) >= 0.5f)
                            && !Physics2D.Linecast(center, playerPosition, this.groundMask);
                    }
                }
                else if (this.actionState == ActionState.Attack && this.actionTimer > 0f)
                {
                    float progress = 1f - this.actionTimer / AttackTime;
                    UpdateWeapon(Mathf.Lerp(60f, -60f, progress), true);
                }
                else if (this.actionTimer <= 0f)
                {
                    if (this.weaponRenderer != null) this.weaponRenderer.enabled = false;
                    if (this.attackArc != null) this.attackArc.enabled = false;
                    this.transform.localScale = this.originalScale;
                    this.spriteRenderer.color = this.originalColor;
                    this.actionState = this.actionState == ActionState.Attack ? ActionState.Recovery : ActionState.Moving;
                    this.actionTimer = RecoveryTime;
                }
                return false;
            }

            float dx = playerPosition.x - this.transform.position.x;
            Vector2 playerOffset = playerPosition - (Vector2)this.transform.position;
            Bounds targetBounds = new Bounds(playerPosition + Vector2.down * 0.25f,
                new Vector3(0.8f, 1.1f, 1f));
            Vector2 nearestPoint = targetBounds.ClosestPoint(this.transform.position);
            bool overhead = !playerOnGround && playerOffset.y > 0.25f
                && (nearestPoint - (Vector2)this.transform.position).sqrMagnitude <= this.attackRange * this.attackRange
                && Mathf.Abs(playerOffset.x) <= this.attackRange * 0.75f
                && !Physics2D.Linecast(this.transform.position, playerPosition, this.groundMask);
            bool chase = playerOnGround && Mathf.Abs(dx) <= this.detectionRange && IsSamePlatform(playerPosition);
            if (overhead || (chase && Mathf.Abs(dx) <= this.attackRange * 0.8f))
            {
                this.attackDirection = dx >= 0f ? 1 : -1;
                this.attackAim = overhead ? Vector2.up : Vector2.right * this.attackDirection;
                this.actionState = ActionState.Windup;
                this.actionTimer = this.windupTime;
                return false;
            }
            float targetX = chase ? playerPosition.x : this.startPosition.x + this.patrolDirection * this.patrolDistance;
            float remaining = (chase ? this.chaseSpeed : this.patrolSpeed * 0.35f) * deltaTime;
            while (remaining > 0f)
            {
                Vector3 position = this.transform.position;
                float step = Mathf.Min(remaining, 0.08f);
                float nextX = Mathf.MoveTowards(position.x, targetX, step);
                if (nextX == position.x || !CanPatrolTo(nextX - position.x))
                {
                    if (!chase) this.patrolDirection *= -1;
                    return false;
                }

                position.x = nextX;
                this.transform.position = position;
                remaining -= step;
            }
            return false;
        }

        public bool TryGetClashBlade(out Vector2 root, out Vector2 tip)
        {
            root = Vector2.zero;
            tip = Vector2.zero;
            if (this.ranged || this.isDead || !this.isActiveAndEnabled ||
                this.actionState != ActionState.Attack || this.weaponRenderer == null || !this.weaponRenderer.enabled)
            {
                return false;
            }

            root = this.weaponRenderer.transform.position;
            tip = this.weaponRenderer.transform.TransformPoint(new Vector3(0.43f, 0.43f, 0f));
            return true;
        }

        public void CancelAttackFromClash()
        {
            this.actionState = ActionState.Recovery;
            this.actionTimer = 0.18f;
            this.transform.localScale = this.originalScale;
            this.transform.localRotation = this.originalRotation;
            this.spriteRenderer.color = this.originalColor;
            if (this.weaponRenderer != null) this.weaponRenderer.enabled = false;
            if (this.attackArc != null) this.attackArc.enabled = false;
        }

        private void EnterStagger(float duration, Vector2 attackerPosition)
        {
            // Lean away from the attacker, independently of patrol direction.
            this.staggerTiltDirection = attackerPosition.x < this.transform.position.x ? -1 : 1;
            this.actionState = ActionState.Stagger;
            this.actionTimer = duration;
            this.staggerDuration = duration;
            this.transform.localScale = this.originalScale;
            this.transform.localRotation = this.originalRotation;
            this.spriteRenderer.color = Color.white;
            if (this.weaponRenderer != null) this.weaponRenderer.enabled = false;
            if (this.attackArc != null) this.attackArc.enabled = false;
        }

        private void CreateRangedVisuals()
        {
            this.projectileTexture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            this.projectileTexture.filterMode = FilterMode.Point;
            Color[] pixels = new Color[256];
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    float radius = new Vector2(x - 7.5f, y - 7.5f).magnitude;
                    pixels[y * 16 + x] = radius <= 7f ? new Color(1f, 0.55f, 0.12f, 1f) : Color.clear;
                }
            }
            this.projectileTexture.SetPixels(pixels);
            this.projectileTexture.Apply();
            this.projectileSprite = Sprite.Create(this.projectileTexture, new Rect(0, 0, 16, 16),
                new Vector2(0.5f, 0.5f), 16f);
            GameObject bullet = new GameObject("Projectile");
            bullet.transform.SetParent(this.transform, false);
            this.projectileRenderer = bullet.AddComponent<SpriteRenderer>();
            this.projectileRenderer.sprite = this.projectileSprite;
            this.projectileRenderer.sortingLayerName = "Player";
            this.projectileRenderer.sortingOrder = 7;
            this.projectileRenderer.enabled = false;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                GameObject line = new GameObject("AimGuide");
                line.transform.SetParent(this.transform, false);
                this.sightLine = line.AddComponent<LineRenderer>();
                this.sightMaterial = new Material(shader);
                this.sightLine.material = this.sightMaterial;
                this.sightLine.useWorldSpace = true;
                this.sightLine.positionCount = 2;
                this.sightLine.startWidth = 0.045f;
                this.sightLine.endWidth = 0.045f;
                this.sightLine.startColor = new Color(1f, 0.1f, 0.1f, 0.85f);
                this.sightLine.endColor = new Color(1f, 0.1f, 0.1f, 0.55f);
                this.sightLine.sortingLayerName = "Player";
                this.sightLine.sortingOrder = 6;
                this.sightLine.enabled = false;
            }
        }

        private bool TickRanged(float deltaTime, Vector2 playerPosition)
        {
            Vector2 origin = this.transform.position;
            Vector2 toPlayer = playerPosition - origin;
            if (this.hasPreviousPlayerPosition && deltaTime > 0f)
            {
                Vector2 movement = playerPosition - this.previousPlayerPosition;
                Vector2 velocity = movement.magnitude > 2f ? Vector2.zero : movement / deltaTime;
                this.estimatedPlayerVelocity = Vector2.Lerp(this.estimatedPlayerVelocity,
                    Vector2.ClampMagnitude(velocity, 10f), Mathf.Clamp01(deltaTime * 12f));
            }
            this.previousPlayerPosition = playerPosition;
            this.hasPreviousPlayerPosition = true;

            float distanceToPlayer = toPlayer.magnitude;
            bool clearSight = distanceToPlayer > 0.0001f
                && !Physics2D.CircleCast(origin, this.projectileRadius, toPlayer.normalized,
                    distanceToPlayer, this.groundMask);

            if (this.rangedState == RangedState.Idle)
            {
                // The target must stay inside the inner range briefly before aiming.
                if (clearSight && distanceToPlayer <= this.rangedSight * 0.9f)
                {
                    this.acquisitionTimer += deltaTime;
                    if (this.acquisitionTimer >= this.targetAcquisitionTime)
                    {
                        this.rangedState = RangedState.Aiming;
                        this.rangedTimer = this.aimDuration;
                        this.acquisitionTimer = 0f;
                    }
                }
                else
                {
                    this.acquisitionTimer = 0f;
                }
            }
            if (this.rangedState == RangedState.Aiming)
            {
                // Keep aiming through small movements beyond the acquisition boundary.
                if (!clearSight || distanceToPlayer > this.rangedSight * 1.1f)
                {
                    HideShot();
                    this.rangedState = RangedState.Cooldown;
                    this.rangedTimer = this.shotCooldown;
                    return false;
                }
                this.shotOrigin = origin;
                float flightTime = Mathf.Min(distanceToPlayer / Mathf.Max(this.projectileSpeed, 0.1f), 0.3f);
                Vector2 lead = Vector2.ClampMagnitude(this.estimatedPlayerVelocity * flightTime, 1.5f);
                Vector2 aim = toPlayer + lead;
                if (aim.sqrMagnitude < 0.0001f ||
                    Physics2D.CircleCast(origin, this.projectileRadius, aim.normalized, aim.magnitude, this.groundMask))
                {
                    aim = toPlayer;
                }
                this.shotDirection = aim.normalized;
                RaycastHit2D wall = Physics2D.CircleCast(origin, this.projectileRadius,
                    this.shotDirection, this.rangedSight * 1.1f, this.groundMask);
                this.shotDistance = wall ? wall.distance : this.rangedSight * 1.1f;
                DrawSight(this.shotOrigin, this.shotOrigin + this.shotDirection * this.shotDistance);
                this.rangedTimer -= deltaTime;
                if (this.rangedTimer <= 0f)
                {
                    this.rangedState = RangedState.Flying;
                    this.projectilePosition = this.shotOrigin;
                    this.projectileTravel = 0f;
                    this.projectileRenderer.transform.localScale = Vector3.one * (this.projectileRadius * 2f);
                    this.projectileRenderer.transform.position = this.projectilePosition;
                    this.projectileRenderer.enabled = true;
                }
            }
            else if (this.rangedState == RangedState.Flying)
            {
                float distance = Mathf.Min(this.projectileSpeed * deltaTime,
                    this.shotDistance - this.projectileTravel);
                Vector2 next = this.projectilePosition + this.shotDirection * distance;
                Bounds playerBounds = new Bounds(playerPosition + Vector2.down * 0.25f,
                    new Vector3(0.8f + this.projectileRadius * 2f, 1.1f + this.projectileRadius * 2f, 1f));
                RaycastHit2D wall = Physics2D.CircleCast(this.projectilePosition, this.projectileRadius,
                    this.shotDirection, distance, this.groundMask);
                Ray ray = new Ray(new Vector3(this.projectilePosition.x, this.projectilePosition.y, 0f),
                    new Vector3(this.shotDirection.x, this.shotDirection.y, 0f));
                float hitDistance;
                bool hitPlayer = playerBounds.Contains(this.projectilePosition)
                    || (playerBounds.IntersectRay(ray, out hitDistance) && hitDistance <= distance
                        && (!wall || hitDistance <= wall.distance));
                this.projectilePosition = next;
                this.projectileTravel += distance;
                this.projectileRenderer.transform.position = next;
                if (hitPlayer || wall || this.projectileTravel >= this.shotDistance - 0.001f)
                {
                    HideShot();
                    this.rangedState = RangedState.Cooldown;
                    this.rangedTimer = this.shotCooldown;
                    return hitPlayer;
                }
            }
            else if (this.rangedState == RangedState.Cooldown)
            {
                this.rangedTimer -= deltaTime;
                if (this.rangedTimer <= 0f) this.rangedState = RangedState.Idle;
            }
            return false;
        }

        private void DrawSight(Vector2 start, Vector2 end)
        {
            if (this.sightLine == null) return;
            this.sightLine.enabled = true;
            this.sightLine.SetPosition(0, start);
            this.sightLine.SetPosition(1, end);
        }

        private void HideShot()
        {
            if (this.sightLine != null) this.sightLine.enabled = false;
            if (this.projectileRenderer != null) this.projectileRenderer.enabled = false;
        }

        private void CreateWeapon()
        {
            Texture2D texture = Resources.Load<Texture2D>("Weapons/knife");
            if (texture != null)
            {
                this.weaponSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.21f, 0.26f), 16f);
                GameObject weapon = new GameObject("EnemyWeapon");
                weapon.transform.SetParent(this.transform, false);
                this.weaponRenderer = weapon.AddComponent<SpriteRenderer>();
                this.weaponRenderer.sprite = this.weaponSprite;
                this.weaponRenderer.sortingLayerName = "Player";
                this.weaponRenderer.sortingOrder = 6;
                weapon.transform.localScale = Vector3.one * 1.8f;
                this.weaponRenderer.enabled = false;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                GameObject arc = new GameObject("EnemyAttackRange");
                arc.transform.SetParent(this.transform, false);
                this.attackArc = arc.AddComponent<LineRenderer>();
                this.arcMaterial = new Material(shader);
                this.attackArc.material = this.arcMaterial;
                this.attackArc.useWorldSpace = false;
                this.attackArc.positionCount = 25;
                this.attackArc.startWidth = 0.07f;
                this.attackArc.endWidth = 0.07f;
                this.attackArc.startColor = new Color(1f, 0.65f, 0.2f, 0.9f);
                this.attackArc.endColor = new Color(1f, 0.3f, 0.1f, 0.8f);
                this.attackArc.sortingLayerName = "Player";
                this.attackArc.sortingOrder = 7;
                this.attackArc.enabled = false;
            }
        }

        private void UpdateWeapon(float angle, bool showArc)
        {
            if (this.weaponRenderer != null)
            {
                this.weaponRenderer.enabled = true;
                this.weaponRenderer.transform.localPosition = new Vector3(this.attackAim.x * 0.4f, this.attackAim.y * 0.4f, 0f);
                float aimAngle = Mathf.Atan2(this.attackAim.y, this.attackAim.x) * Mathf.Rad2Deg;
                float worldAngle = aimAngle + (this.attackDirection > 0 ? angle : -angle);
                this.weaponRenderer.transform.rotation = Quaternion.Euler(0f, 0f, worldAngle - 45f);
            }
            if (this.attackArc != null)
            {
                this.attackArc.enabled = showArc;
                if (showArc)
                {
                    for (int i = 0; i < this.attackArc.positionCount; i++)
                    {
                        float radians = Mathf.Lerp(-60f, 60f, i / 24f) * Mathf.Deg2Rad;
                        float aimAngle = Mathf.Atan2(this.attackAim.y, this.attackAim.x);
                        Vector3 worldOffset = new Vector3(Mathf.Cos(aimAngle + radians),
                            Mathf.Sin(aimAngle + radians), 0f) * this.attackRange;
                        this.attackArc.SetPosition(i, this.transform.InverseTransformVector(worldOffset));
                    }
                }
            }
        }

        public void ResetForRestart()
        {
            HideShot();
            this.rangedState = RangedState.Idle;
            this.rangedTimer = 0f;
            this.acquisitionTimer = 0f;
            this.hasPreviousPlayerPosition = false;
            this.estimatedPlayerVelocity = Vector2.zero;
            this.projectileTravel = 0f;
            this.transform.position = this.startPosition;
            this.transform.localScale = this.originalScale;
            this.transform.localRotation = this.originalRotation;
            this.spriteRenderer.sprite = this.originalSprite;
            this.spriteRenderer.color = this.originalColor;
            this.currentHealth = MaxHealth;
            this.isDead = false;
            this.deathTimer = 0f;
            this.actionState = ActionState.Moving;
            this.actionTimer = 0f;
            this.patrolDirection = 1;
            this.attackDirection = 0;
            this.attackAim = Vector2.zero;
            if (this.weaponRenderer != null) this.weaponRenderer.enabled = false;
            if (this.attackArc != null) this.attackArc.enabled = false;
            if (this.enemySprite != null)
            {
                Destroy(this.enemySprite);
                this.enemySprite = null;
            }
            if (this.spriteTexture != null)
            {
                Destroy(this.spriteTexture);
                this.spriteTexture = null;
            }
            UpdateHealthBar();
            if (this.healthBarRoot != null) this.healthBarRoot.SetActive(true);
            this.gameObject.SetActive(true);
        }

        private bool IsSamePlatform(Vector2 playerPosition)
        {
            Vector2 enemyFeet = new Vector2(this.transform.position.x, this.spriteRenderer.bounds.min.y + 0.05f);
            Vector2 playerFeet = playerPosition + Vector2.down * 0.75f;
            RaycastHit2D enemyFloor = Physics2D.Raycast(enemyFeet, Vector2.down, 0.3f, this.groundMask);
            RaycastHit2D playerFloor = Physics2D.Raycast(playerFeet + Vector2.up * 0.1f, Vector2.down, 0.4f, this.groundMask);
            return enemyFloor && playerFloor && enemyFloor.collider == playerFloor.collider
                && !Physics2D.Linecast(this.transform.position, playerPosition, this.groundMask);
        }

        private bool CanPatrolTo(float moveX)
        {
            Bounds body = this.spriteRenderer.bounds;
            Vector2 direction = Vector2.right * Mathf.Sign(moveX);
            Vector2 size = new Vector2(body.size.x, Mathf.Max(body.size.y - 0.04f, 0.01f));
            if (Physics2D.BoxCast(body.center, size, 0f, direction, Mathf.Abs(moveX), this.groundMask))
            {
                return false;
            }

            // Check both ends of the feet at the next position without changing height.
            float halfFootWidth = body.extents.x * 0.8f;
            Vector2 feet = new Vector2(body.center.x + moveX, body.min.y + 0.04f);
            return Physics2D.Raycast(feet - Vector2.right * halfFootWidth, Vector2.down, 0.17f, this.groundMask)
                && Physics2D.Raycast(feet + Vector2.right * halfFootWidth, Vector2.down, 0.17f, this.groundMask);
        }

        public void TakeDamage(int damage, Vector2 attackerPosition)
        {
            if (this.isDead || damage <= 0)
            {
                return;
            }

            this.currentHealth = Mathf.Max(this.currentHealth - damage, 0);
            UpdateHealthBar();
            if (this.currentHealth > 0 && !this.ranged)
            {
                EnterStagger(damage >= 20 ? HeavyStaggerTime : HitStaggerTime, attackerPosition);
            }
            if (this.currentHealth == 0)
            {
                HideShot();
                this.transform.localScale = this.originalScale;
                this.transform.localRotation = this.originalRotation;
                this.spriteRenderer.color = this.originalColor;
                this.isDead = true;
                if (this.weaponRenderer != null) this.weaponRenderer.enabled = false;
                if (this.attackArc != null) this.attackArc.enabled = false;
                this.deathTimer = DeathDisplayTime;
                if (this.healthBarRoot != null)
                {
                    this.healthBarRoot.SetActive(false);
                }
                DrawDeadFace();
            }
        }

        private void CreateHealthBar()
        {
            this.healthBarTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            this.healthBarTexture.name = "EnemyHealthBarPixel";
            this.healthBarTexture.SetPixel(0, 0, Color.white);
            this.healthBarTexture.Apply();
            this.healthBarSprite = Sprite.Create(this.healthBarTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f);

            this.healthBarRoot = new GameObject("HealthBar");
            this.healthBarRoot.transform.SetParent(this.transform, false);
            this.healthBarRoot.transform.localPosition = new Vector3(0f, 0.72f, 0f);

            CreateHealthBarPart("Border", this.healthBarRoot.transform,
                new Vector3(1.22f, 0.2f, 1f), 3, new Color(0.07f, 0.06f, 0.08f, 1f));
            CreateHealthBarPart("Background", this.healthBarRoot.transform,
                new Vector3(1.14f, 0.12f, 1f), 4, new Color(0.3f, 0.06f, 0.08f, 1f));
            this.healthBarFill = CreateHealthBarPart("Fill", this.healthBarRoot.transform,
                new Vector3(HealthBarWidth, 0.06f, 1f), 5, new Color(1f, 0.24f, 0.18f, 1f));
            UpdateHealthBar();
        }

        private SpriteRenderer CreateHealthBarPart(string partName, Transform parent, Vector3 scale,
            int sortingOrder, Color color)
        {
            GameObject part = new GameObject(partName);
            part.transform.SetParent(parent, false);
            part.transform.localScale = scale;
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = this.healthBarSprite;
            renderer.color = color;
            renderer.sortingLayerName = "Player";
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void UpdateHealthBar()
        {
            if (this.healthBarFill == null)
            {
                return;
            }

            float ratio = Mathf.Clamp01((float)this.currentHealth / MaxHealth);
            float fillWidth = HealthBarWidth * ratio;
            this.healthBarFill.transform.localScale = new Vector3(fillWidth, 0.06f, 1f);
            this.healthBarFill.transform.localPosition = new Vector3(-(HealthBarWidth - fillWidth) * 0.5f,
                0f, 0f);
        }

        private void DrawDeadFace()
        {
            this.spriteTexture = CreateEnemyTexture(this.ranged);
            this.enemySprite = Sprite.Create(this.spriteTexture, new Rect(0f, 0f, SpriteSize, SpriteSize),
                new Vector2(0.5f, 0.5f), SpriteSize);
            this.spriteRenderer.sprite = this.enemySprite;

            Color32 xColor = new Color32(255, 244, 220, 255);
            for (int i = 0; i < 4; i++)
            {
                this.spriteTexture.SetPixel(4 + i, 9 + i, xColor);
                this.spriteTexture.SetPixel(4 + i, 12 - i, xColor);
                this.spriteTexture.SetPixel(9 + i, 9 + i, xColor);
                this.spriteTexture.SetPixel(9 + i, 12 - i, xColor);
            }
            this.spriteTexture.Apply();
        }

        private static Texture2D CreateEnemyTexture(bool orange)
        {
            Texture2D texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
            texture.name = "RedEnemyBlockTexture";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color32[] pixels = new Color32[SpriteSize * SpriteSize];
            Color32 outline = orange ? new Color32(112, 49, 15, 255) : new Color32(92, 18, 28, 255);
            Color32 red = orange ? new Color32(242, 143, 39, 255) : new Color32(224, 47, 55, 255);
            Color32 eyeWhite = new Color32(255, 248, 224, 255);
            Color32 pupil = new Color32(28, 20, 28, 255);
            Color32 nose = new Color32(255, 185, 104, 255);
            Color32 mouth = new Color32(78, 20, 28, 255);

            FillRect(pixels, 1, 1, 14, 14, outline);
            FillRect(pixels, 2, 2, 12, 12, red);

            // Two square eyes with dark pupils.
            FillRect(pixels, 4, 9, 3, 3, eyeWhite);
            FillRect(pixels, 9, 9, 3, 3, eyeWhite);
            FillRect(pixels, 5, 9, 1, 2, pupil);
            FillRect(pixels, 10, 9, 1, 2, pupil);

            // A small warm-colored nose and a short mouth line.
            FillRect(pixels, 7, 7, 2, 2, nose);
            FillRect(pixels, 6, 4, 4, 1, mouth);

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static void FillRect(Color32[] pixels, int x, int y, int width, int height, Color32 color)
        {
            for (int py = y; py < y + height; py++)
            {
                for (int px = x; px < x + width; px++)
                {
                    pixels[py * SpriteSize + px] = color;
                }
            }
        }

        private void OnDestroy()
        {
            if (this.projectileSprite != null) Destroy(this.projectileSprite);
            if (this.projectileTexture != null) Destroy(this.projectileTexture);
            if (this.sightMaterial != null) Destroy(this.sightMaterial);
            if (this.weaponSprite != null) Destroy(this.weaponSprite);
            if (this.arcMaterial != null) Destroy(this.arcMaterial);
            if (this.enemySprite != null)
            {
                Destroy(this.enemySprite);
            }
            if (this.spriteTexture != null)
            {
                Destroy(this.spriteTexture);
            }
            if (this.healthBarSprite != null)
            {
                Destroy(this.healthBarSprite);
            }
            if (this.healthBarTexture != null)
            {
                Destroy(this.healthBarTexture);
            }
        }
    }
}
