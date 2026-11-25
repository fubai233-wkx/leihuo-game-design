
using DG.Tweening;
using Myd.Common;
using Myd.Platform.Core;
using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Myd.Platform
{
    enum EGameState
    {
        Load,   //加载中
        Play,   //游戏中
        Pause,  //游戏暂停
        Fail,   //游戏失败
    }
    public class Game : MonoBehaviour, IGameContext
    {
        public static Game Instance;

        [SerializeField]
        public Level level;
        //场景特效管理器
        [SerializeField]
        private SceneEffectManager sceneEffectManager;
        [SerializeField]
        private SceneCamera gameCamera;
        //玩家
        Player player;
        private SpriteRenderer[] spikeRenderers;
        private EnemyBlock[] enemies;
        private Scene originalScene;
        private Scene bossScene;
        private ArenaBoss arenaBoss;
        private Bounds originalBounds;
        private Vector2 originalStart;
        private bool bossDefeated;
        private bool inBossBattle;

        private const int MaxHealth = 3;
        private const float HitInvulnerabilityTime = 1f;
        private int currentHealth;
        private float hitInvulnerabilityTimer;
        private Texture2D heartTexture;
        private GUIStyle restartMessageStyle;
        private const float ComboWindow = 0.45f;
        private const float ComboRecovery = 0.35f;
        private const float GroundHeavyAdvance = 0.55f;
        private const float AirHeavyAdvance = 0.1375f;
        private int comboStep;
        private int swingStep;
        private int swingFacing;
        private float comboWindowTimer;
        private float comboRecoveryTimer;
        private float swingDuration;
        private float heavyAdvanceDistance;
        private bool swingHitApplied;
        private bool swingQueued;
        private bool swingClashed;
        private int selectedWeapon = 1;
        private float knifeSwingTimer;
        private Texture2D knifeTexture;
        private Sprite knifeSprite;
        private SpriteRenderer knifeRenderer;
        private AudioSource weaponAudioSource;
        private AudioClip slashSound;

        EGameState gameState;

        void Awake()
        {
            Instance = this;
            originalScene = SceneManager.GetActiveScene();

            gameState = EGameState.Load;
            currentHealth = MaxHealth;
            heartTexture = CreateHeartTexture();
            LoadWeaponSprites();

            player = new Player(this);

            List<SpriteRenderer> spikes = new List<SpriteRenderer>();
            foreach (SpriteRenderer spriteRenderer in FindObjectsOfType<SpriteRenderer>())
            {
                if (spriteRenderer.gameObject.name.IndexOf("Spike", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    spikes.Add(spriteRenderer);
                }
            }
            spikeRenderers = spikes.ToArray();

        }

        IEnumerator Start()
        {
            yield return null;

            enemies = FindObjectsOfType<EnemyBlock>();
            //加载玩家
            player.Reload(level.Bounds, level.StartPosition);
            this.gameState = EGameState.Play;
            yield return null;
        }

        public void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            if (hitInvulnerabilityTimer > 0f)
            {
                hitInvulnerabilityTimer = Mathf.Max(hitInvulnerabilityTimer - deltaTime, 0f);
            }

            if (UpdateTime(deltaTime))
            {
                if (this.gameState == EGameState.Play)
                {
                    GameInput.Update(deltaTime);
                    player.Update(deltaTime, enemies, inBossBattle ? arenaBoss : null);
                    if (!inBossBattle && !bossDefeated && player.Position.x < level.StartPosition.x - 7f)
                    {
                        EnterBossBattle();
                        return;
                    }
                    UpdateWeaponInput(deltaTime);
                    ResolveClashes();
                    foreach (EnemyBlock enemy in enemies)
                    {
                        if (!inBossBattle && enemy != null && enemy.isActiveAndEnabled)
                        {
                            bool enemyHit = enemy.Tick(deltaTime, player.Position, player.OnGround);
                            bool clashed = ResolveClashes();
                            if (enemyHit && !clashed && hitInvulnerabilityTimer <= 0f)
                            {
                                TakeDamage();
                            }
                        }
                    }
                    if (inBossBattle && arenaBoss != null)
                    {
                        if (arenaBoss.Tick(deltaTime, player.Position) && hitInvulnerabilityTimer <= 0f)
                        {
                            TakeDamage();
                        }
                        if (arenaBoss.VictoryComplete)
                        {
                            LeaveBossBattle();
                            return;
                        }
                    }
                    if (this.gameState != EGameState.Play)
                    {
                        player.SetDamageFlash(false);
                        return;
                    }
                    UpdateWeaponVisuals(deltaTime);

                    if (hitInvulnerabilityTimer <= 0f &&
                        !inBossBattle && player.TouchesSpike(spikeRenderers))
                    {
                        TakeDamage();
                    }

                    player.SetDamageFlash(this.gameState == EGameState.Play && hitInvulnerabilityTimer > 0f &&
                        Mathf.FloorToInt(hitInvulnerabilityTimer * 10f) % 2 == 0);

                    //更新摄像机
                    gameCamera.SetCameraPosition(player.GetCameraPosition());
                }
            }
        }

        private void EnterBossBattle()
        {
            originalBounds = level.Bounds;
            originalStart = level.StartPosition;
            bossScene = SceneManager.CreateScene("BossArena");
            SceneManager.SetActiveScene(bossScene);
            arenaBoss = new ArenaBoss(bossScene);
            inBossBattle = true;
            level.Bounds = ArenaBoss.ArenaBounds;
            level.StartPosition = ArenaBoss.PlayerSpawn;
            ResetCombo();
            hitInvulnerabilityTimer = 1f;
            player.Reload(level.Bounds, level.StartPosition);
            gameCamera.SetCameraPosition(player.GetCameraPosition());
        }

        private void LeaveBossBattle()
        {
            bossDefeated = true;
            inBossBattle = false;
            SceneManager.SetActiveScene(originalScene);
            arenaBoss.Dispose();
            arenaBoss = null;
            SceneManager.UnloadSceneAsync(bossScene);
            level.Bounds = originalBounds;
            level.StartPosition = originalStart;
            ResetCombo();
            currentHealth = MaxHealth;
            hitInvulnerabilityTimer = 1f;
            player.Reload(level.Bounds, level.StartPosition);
            gameCamera.SetCameraPosition(player.GetCameraPosition());
        }

        private void UpdateWeaponInput(float deltaTime)
        {
            comboRecoveryTimer = Mathf.Max(0f, comboRecoveryTimer - deltaTime);
            if (knifeSwingTimer <= 0f && comboStep > 0 && comboStep < 3)
            {
                comboWindowTimer -= deltaTime;
                if (comboWindowTimer <= 0f) comboStep = 0;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                selectedWeapon = 1;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                selectedWeapon = 2;
                ResetCombo();
            }

            if (selectedWeapon != 1 || !Input.GetMouseButtonDown(0) || comboRecoveryTimer > 0f || !player.CanAttack)
            {
                return;
            }

            if (knifeSwingTimer > 0f)
            {
                if (swingStep < 3) swingQueued = true;
                return;
            }
            StartKnifeSwing();
        }

        private void StartKnifeSwing()
        {
            comboStep = comboStep % 3 + 1;
            swingStep = comboStep;
            swingFacing = player.Facing;
            heavyAdvanceDistance = player.OnGround ? GroundHeavyAdvance : AirHeavyAdvance;
            swingDuration = swingStep == 3 ? 0.38f : 0.2f;
            knifeSwingTimer = swingDuration;
            swingHitApplied = false;
            swingQueued = false;
            swingClashed = false;
            ShowKnifeSwing();
        }

        private bool ResolveClashes()
        {
            if (swingClashed || knifeSwingTimer <= 0f || swingDuration <= 0f) return false;
            float progress = 1f - knifeSwingTimer / swingDuration;
            if (progress < 0.15f || progress > 0.85f) return false;

            Vector2 playerRoot = player.Position + new Vector2(swingFacing * 0.4f, -0.25f);
            float angle = swingStep == 3 ? Mathf.Lerp(100f, -70f, progress) : Mathf.Lerp(50f, -45f, progress);
            float worldAngle = swingFacing > 0 ? angle : 180f - angle;
            Quaternion rotation = Quaternion.Euler(0f, 0f, worldAngle - 45f);
            float scale = swingStep == 3 ? 3.1f : 2.2f;
            Vector2 playerTip = playerRoot + (Vector2)(rotation * (new Vector3(0.43f, 0.43f, 0f) * scale));
            foreach (EnemyBlock enemy in enemies)
            {
                Vector2 enemyRoot;
                Vector2 enemyTip;
                if (!inBossBattle && enemy != null && enemy.TryGetClashBlade(out enemyRoot, out enemyTip) &&
                    BladesTouch(playerRoot, playerTip, enemyRoot, enemyTip, 0.18f) &&
                    !Physics2D.Linecast(playerRoot, enemyRoot, LayerMask.GetMask("Ground")))
                {
                    swingClashed = true;
                    enemy.CancelAttackFromClash();
                    hitInvulnerabilityTimer = Mathf.Max(hitInvulnerabilityTimer, 0.25f);
                    Freeze(0.035f);
                    CameraShake(Vector2.right * swingFacing, 0.08f);
                    return true;
                }
            }
            return false;
        }

        private static bool BladesTouch(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float thickness)
        {
            Vector2 ab = b - a;
            Vector2 cd = d - c;
            float cross = ab.x * cd.y - ab.y * cd.x;
            if (Mathf.Abs(cross) > 0.00001f)
            {
                Vector2 ac = c - a;
                float t = (ac.x * cd.y - ac.y * cd.x) / cross;
                float u = (ac.x * ab.y - ac.y * ab.x) / cross;
                if (t >= 0f && t <= 1f && u >= 0f && u <= 1f) return true;
            }
            float limit = thickness * thickness;
            return PointSegmentDistanceSquared(a, c, d) <= limit ||
                   PointSegmentDistanceSquared(b, c, d) <= limit ||
                   PointSegmentDistanceSquared(c, a, b) <= limit ||
                   PointSegmentDistanceSquared(d, a, b) <= limit;
        }

        private static float PointSegmentDistanceSquared(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float length = segment.sqrMagnitude;
            if (length < 0.000001f) return (point - start).sqrMagnitude;
            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / length);
            return (point - (start + segment * t)).sqrMagnitude;
        }

        private void ApplyKnifeHit()
        {
            float reach = swingStep == 3 ? 2.1f : 1.35f;
            float halfAngle = swingStep == 3 ? 70f : 48f;
            int damage = swingStep == 3 ? 20 : 10;
            Vector2 facingDirection = Vector2.right * swingFacing;
            float minimumDot = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
            bool hitEnemy = false;
            foreach (EnemyBlock enemy in enemies)
            {
                if (inBossBattle || enemy == null || !enemy.isActiveAndEnabled || !enemy.IsAlive)
                {
                    continue;
                }

                Vector2 targetPoint = enemy.Bounds.ClosestPoint(player.Position);
                Vector2 toTarget = targetPoint - player.Position;
                if (toTarget.sqrMagnitude <= reach * reach &&
                    (toTarget.sqrMagnitude < 0.0001f || Vector2.Dot(toTarget.normalized, facingDirection) >= minimumDot))
                {
                    enemy.TakeDamage(damage, player.Position);
                    hitEnemy = true;
                }
            }
            if (inBossBattle && arenaBoss != null && !arenaBoss.Defeated)
            {
                Vector2 target = arenaBoss.BodyBounds.ClosestPoint(player.Position);
                Vector2 toBoss = target - player.Position;
                if (toBoss.sqrMagnitude <= reach * reach &&
                    (toBoss.sqrMagnitude < 0.0001f || Vector2.Dot(toBoss.normalized, facingDirection) >= minimumDot) &&
                    !Physics2D.Linecast(player.Position, target, LayerMask.GetMask("Ground")))
                {
                    arenaBoss.TakeDamage(damage);
                    hitEnemy = true;
                }
            }
            if (hitEnemy && swingStep == 3)
            {
                Freeze(0.035f);
            }
        }

        private void ResetCombo()
        {
            comboStep = 0;
            swingStep = 0;
            swingQueued = false;
            knifeSwingTimer = 0f;
            comboWindowTimer = 0f;
            if (knifeRenderer != null) knifeRenderer.enabled = false;
        }

        private void LoadWeaponSprites()
        {
            this.knifeTexture = Resources.Load<Texture2D>("Weapons/knife");
            this.slashSound = Resources.Load<AudioClip>(
                "Weapons/SlashPack/sword-slash-pixel-fx-v1/sounds/fx-slash-swing");
            if (this.knifeTexture != null)
            {
                this.knifeTexture.filterMode = FilterMode.Point;
                // The gold lower endpoint is the fixed root; the opposite gold endpoint is the tip.
                this.knifeSprite = Sprite.Create(this.knifeTexture,
                    new Rect(0f, 0f, this.knifeTexture.width, this.knifeTexture.height),
                    new Vector2(0.21f, 0.26f), 16f);
            }
            if (this.knifeSprite != null)
            {
                GameObject knifeObject = new GameObject("KnifeSwingVisual");
                this.knifeRenderer = knifeObject.AddComponent<SpriteRenderer>();
                this.knifeRenderer.sprite = this.knifeSprite;
                this.knifeRenderer.sortingLayerName = "Player";
                this.knifeRenderer.sortingOrder = 18;
                knifeObject.transform.localScale = Vector3.one * 2.6f;
                this.knifeRenderer.enabled = false;
            }
        }

        private void ShowKnifeSwing()
        {
            if (this.slashSound != null)
            {
                if (this.weaponAudioSource == null)
                {
                    this.weaponAudioSource = this.gameObject.AddComponent<AudioSource>();
                    this.weaponAudioSource.playOnAwake = false;
                }
                this.weaponAudioSource.PlayOneShot(this.slashSound);
            }
        }

        private void UpdateWeaponVisuals(float deltaTime)
        {
            if (this.knifeSwingTimer <= 0f)
            {
                return;
            }

            float previousProgress = 1f - knifeSwingTimer / swingDuration;
            knifeSwingTimer = Mathf.Max(knifeSwingTimer - deltaTime, 0f);
            float progress = 1f - knifeSwingTimer / swingDuration;
            if (swingStep == 3)
            {
                float from = Mathf.Clamp01((previousProgress - 0.15f) / 0.55f);
                float to = Mathf.Clamp01((progress - 0.15f) / 0.55f);
                player.MoveForAttack(swingFacing * heavyAdvanceDistance * (to - from));
            }
            if (!swingHitApplied && progress >= 0.35f)
            {
                swingHitApplied = true;
                ApplyKnifeHit();
            }

            int facing = swingFacing;
            // The fixed root is at the midpoint of the character's front side.
            Vector2 weaponRoot = player.Position + new Vector2(facing * 0.4f, -0.25f);
            if (this.knifeRenderer != null)
            {
                this.knifeRenderer.enabled = this.knifeSwingTimer > 0f;
                if (this.knifeSwingTimer > 0f)
                {
                    float sweepAngle = swingStep == 3
                        ? Mathf.Lerp(100f, -70f, progress)
                        : Mathf.Lerp(50f, -45f, progress);
                    float worldAngle = facing > 0 ? sweepAngle : 180f - sweepAngle;
                    this.knifeRenderer.flipX = false;
                    // Keep the hilt at the hand and rotate the blade around that pivot.
                    this.knifeRenderer.transform.position = weaponRoot;
                    // Keep the lower gold endpoint fixed and place the upper endpoint on the radius.
                    this.knifeRenderer.transform.rotation = Quaternion.Euler(0f, 0f, worldAngle - 45f);
                    this.knifeRenderer.transform.localScale = Vector3.one * (swingStep == 3 ? 3.1f : 2.2f);
                }
            }

            if (knifeSwingTimer <= 0f)
            {
                if (knifeRenderer != null) knifeRenderer.enabled = false;
                if (swingStep == 3)
                {
                    comboStep = 0;
                    comboRecoveryTimer = ComboRecovery;
                    swingQueued = false;
                }
                else if (swingQueued && selectedWeapon == 1)
                {
                    StartKnifeSwing();
                }
                else
                {
                    comboWindowTimer = ComboWindow;
                }
            }

        }

        private void TakeDamage()
        {
            if (this.gameState != EGameState.Play || this.hitInvulnerabilityTimer > 0f)
            {
                return;
            }

            currentHealth = Mathf.Max(currentHealth - 1, 0);
            hitInvulnerabilityTimer = HitInvulnerabilityTime;
            ResetCombo();
            comboRecoveryTimer = 0f;
            if (currentHealth == 0)
            {
                player.SetDamageFlash(false);
                this.gameState = EGameState.Fail;
                StartCoroutine(RestartAfterGameOver());
            }
        }

        private IEnumerator RestartAfterGameOver()
        {
            yield return new WaitForSecondsRealtime(1.25f);

            currentHealth = MaxHealth;
            hitInvulnerabilityTimer = HitInvulnerabilityTime;
            foreach (EnemyBlock enemy in enemies)
            {
                if (!inBossBattle && enemy != null)
                {
                    enemy.ResetForRestart();
                }
            }
            if (inBossBattle && arenaBoss != null) arenaBoss.Reset();
            player.Reload(level.Bounds, level.StartPosition);
            this.gameState = EGameState.Play;
        }

        private void OnGUI()
        {
            if (!inBossBattle && !bossDefeated && gameState == EGameState.Play &&
                player != null && player.Position.x < level.StartPosition.x - 3f)
            {
                GUI.Label(new Rect(18f, Screen.height - 48f, 230f, 28f), "<-- BOSS");
            }
            if (inBossBattle && arenaBoss != null)
            {
                float width = Mathf.Min(420f, Screen.width * 0.5f);
                Rect background = new Rect((Screen.width - width) * 0.5f, 25f, width, 20f);
                Color oldColor = GUI.color;
                GUI.color = new Color(0.12f, 0.08f, 0.17f, 0.95f);
                GUI.DrawTexture(background, Texture2D.whiteTexture);
                GUI.color = new Color(0.75f, 0.25f, 0.65f, 1f);
                GUI.DrawTexture(new Rect(background.x + 3f, background.y + 3f,
                    (background.width - 6f) * arenaBoss.Health / ArenaBoss.MaxHealth, background.height - 6f),
                    Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(background.x, background.y + 22f, background.width, 25f),
                    arenaBoss.Defeated ? "VICTORY" : "THE VIOLET WARDEN");
                GUI.color = oldColor;
            }
            if (heartTexture != null)
            {
                Color originalColor = GUI.color;
                for (int i = 0; i < MaxHealth; i++)
                {
                    GUI.color = i < currentHealth
                        ? new Color(1f, 0.2f, 0.25f, 1f)
                        : new Color(0.28f, 0.28f, 0.32f, 0.85f);
                    GUI.DrawTexture(new Rect(18f + i * 42f, 18f, 34f, 30f), heartTexture, ScaleMode.ScaleToFit, true);
                }
                GUI.color = originalColor;
            }

            DrawWeaponSlots();

            if (this.gameState == EGameState.Fail)
            {
                if (restartMessageStyle == null)
                {
                    restartMessageStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 36,
                        fontStyle = FontStyle.Bold
                    };
                    restartMessageStyle.normal.textColor = Color.white;
                }

                GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), "GAME OVER\n重新开始…", restartMessageStyle);
            }
        }

        private void DrawWeaponSlots()
        {
            Color oldColor = GUI.color;
            GUIStyle slotLabel = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            slotLabel.normal.textColor = Color.white;

            float slotWidth = 62f;
            float slotHeight = 56f;
            float gap = 8f;
            float startX = Screen.width - (slotWidth * 2f + gap) - 18f;
            for (int i = 0; i < 2; i++)
            {
                int slotNumber = i + 1;
                Rect slot = new Rect(startX + i * (slotWidth + gap), 18f, slotWidth, slotHeight);
                GUI.color = selectedWeapon == slotNumber
                    ? new Color(1f, 0.82f, 0.25f, 1f)
                    : new Color(0.18f, 0.2f, 0.24f, 0.95f);
                GUI.Box(slot, GUIContent.none);
                GUI.color = Color.white;
                if (slotNumber == 1 && this.knifeTexture != null)
                {
                    GUI.DrawTexture(new Rect(slot.x + 17f, slot.y + 7f, 28f, 28f), this.knifeTexture,
                        ScaleMode.ScaleToFit, true);
                }
                else
                {
                    GUI.Label(new Rect(slot.x, slot.y + 4f, slot.width, 30f), slotNumber == 1 ? "刀" : "空", slotLabel);
                }
                GUI.Label(new Rect(slot.x, slot.y + slotHeight + 1f, slot.width, 20f), slotNumber.ToString(), slotLabel);
            }
            GUI.color = oldColor;
        }

        private static Texture2D CreateHeartTexture()
        {
            string[] rows =
            {
                "001100001100",
                "011110011110",
                "111111111111",
                "111111111111",
                "111111111111",
                "011111111110",
                "001111111100",
                "000111111000",
                "000011110000",
                "000001100000"
            };

            Texture2D texture = new Texture2D(12, 10, TextureFormat.RGBA32, false);
            texture.name = "HealthHeart";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color32[] pixels = new Color32[12 * 10];
            for (int y = 0; y < rows.Length; y++)
            {
                for (int x = 0; x < rows[y].Length; x++)
                {
                    if (rows[rows.Length - 1 - y][x] == '1')
                    {
                        pixels[y * 12 + x] = new Color32(255, 255, 255, 255);
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private void OnDestroy()
        {
            if (heartTexture != null)
            {
                Destroy(heartTexture);
            }
            if (knifeRenderer != null)
            {
                Destroy(knifeRenderer.gameObject);
            }
            if (knifeSprite != null)
            {
                Destroy(knifeSprite);
            }
        }

        #region 冻帧
        private float freezeTime;

        //更新顿帧数据，如果不顿帧，返回true
        public bool UpdateTime(float deltaTime)
        {
            if (freezeTime > 0f)
            {
                freezeTime = Mathf.Max(freezeTime - deltaTime, 0f);
                return false;
            }
            if (Time.timeScale == 0)
            {
                Time.timeScale = 1;
            }
            return true;
        }

        //冻帧
        public void Freeze(float freezeTime)
        {
            this.freezeTime = Mathf.Max(this.freezeTime, freezeTime);
            if (this.freezeTime > 0)
            {
                Time.timeScale = 0;
            }
            else
            {
                Time.timeScale = 1;
            }
        }
        #endregion
        public void CameraShake(Vector2 dir, float duration)
        {
            this.gameCamera.Shake(dir, duration);
        }

        public IEffectControl EffectControl { get=>this.sceneEffectManager; }

        public ISoundControl SoundControl { get; }
        
    }

}
