using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(GunkHackz.Mod), "GunkHackz", "2.1.0", "Me")]

namespace GunkHackz
{
    public class Mod : MelonMod
    {
        private bool showMenu = false;
        private Rect windowRect = new Rect(20, 20, 340, 0);
        private int selectedTab = 0;
        private string[] tabNames = new string[] { "Player", "World", "Enemies", "Powerups" };

        // Time
        private float gameSpeed = 1f;
        private bool freezeTime = false;
        private bool timeWasChanged = false;

        // God mode
        private bool godMode = false;
        private PlayerHealth playerHealth;

        // Movement
        private PlayerMovement playerMovement;
        private float speedMult = 1f;
        private float jumpMult = 1f;
        private float gravityMult = 1f;

        private float baseWalkSpeed, baseSprintSpeed, baseJumpForce;
        private bool gotMovementBases = false;

        private Vector3 baseGravity;
        private bool gotGravityBase = false;

        private FieldInfo jumpField;
        private FieldInfo gravField;
        private MethodInfo jumpMethod;

        // Double jump
        private bool doubleJump = false;
        private int extraJumps = 1;
        private int airJumpsUsed = 0;

        // Powerups
        private PowerUpTypes[] powerupTypes;
        private HashSet<PowerUpTypes> keepActive = new HashSet<PowerUpTypes>();
        private Vector2 powerScroll;
        private float nextPowerTime = 0f;

        // Enemies
        private bool oneHitKills = false;
        private float enemyHealthMult = 1f;
        private Dictionary<EnemyHealth, float> enemyBaseMax = new Dictionary<EnemyHealth, float>();
        private float nextEnemyScan = 0f;

        private float nextSearchTime = 0f;

        private CursorLockMode oldLockState;
        private bool oldCursorVisible;

        // Menu look
        private bool stylesReady = false;
        private GUIStyle windowStyle, labelStyle, titleStyle, dimStyle, valueStyle;
        private GUIStyle toggleStyle, buttonStyle, tabStyle, tabActiveStyle;
        private GUIStyle sliderStyle, thumbStyle, lineStyle;

        public override void OnApplicationStart()
        {
            jumpField = typeof(PlayerMovement).GetField("jumpForce", BindingFlags.Instance | BindingFlags.NonPublic);
            gravField = typeof(PlayerMovement).GetField("startGrav", BindingFlags.Static | BindingFlags.NonPublic);
            jumpMethod = typeof(PlayerMovement).GetMethod("Jump", BindingFlags.Instance | BindingFlags.NonPublic);
            powerupTypes = (PowerUpTypes[])Enum.GetValues(typeof(PowerUpTypes));
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                if (showMenu) CloseMenu();
                else OpenMenu();
            }

            if (showMenu)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            ApplyTime();
            FindPlayerThings();
            ApplyGodMode();
            ApplyMovement();
            ApplyDoubleJump();
            ApplyGravity();
            ApplyKeepActivePowerUps();
            ApplyEnemyCheats();
        }

        private void ApplyTime()
        {
            if (freezeTime)
            {
                Time.timeScale = 0f;
                timeWasChanged = true;
            }
            else if (Mathf.Abs(gameSpeed - 1f) > 0.01f)
            {
                Time.timeScale = gameSpeed;
                timeWasChanged = true;
            }
            else if (timeWasChanged)
            {
                Time.timeScale = 1f;
                timeWasChanged = false;
            }
        }

        private void FindPlayerThings()
        {
            if (Time.unscaledTime < nextSearchTime) return;
            nextSearchTime = Time.unscaledTime + 1f;

            if (playerHealth == null)
                playerHealth = UnityEngine.Object.FindObjectOfType<PlayerHealth>();

            if (playerMovement == null)
            {
                playerMovement = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
                gotMovementBases = false;
            }
        }

        private void ApplyGodMode()
        {
            if (playerHealth == null) return;

            if (godMode)
            {
                playerHealth.isDamageable = false;
                playerHealth.currentHealth = playerHealth.maxHealth;
            }
        }

        private void SetGodMode(bool on)
        {
            godMode = on;
            if (!on && playerHealth != null)
                playerHealth.isDamageable = true;
        }

        private void ApplyMovement()
        {
            if (playerMovement == null || jumpField == null) return;

            if (!gotMovementBases)
            {
                baseWalkSpeed = playerMovement.walkSpeed;
                baseSprintSpeed = playerMovement.sprintSpeed;
                baseJumpForce = (float)jumpField.GetValue(playerMovement);
                gotMovementBases = true;
            }

            playerMovement.walkSpeed = baseWalkSpeed * speedMult;
            playerMovement.sprintSpeed = baseSprintSpeed * speedMult;
            jumpField.SetValue(playerMovement, baseJumpForce * jumpMult);
        }

        private void ApplyDoubleJump()
        {
            if (!doubleJump || playerMovement == null || jumpMethod == null) return;
            if (!playerMovement.canJump || !playerMovement.canMove) return;

            // Touching the ground gives the air jumps back
            if (playerMovement.OnGround())
            {
                airJumpsUsed = 0;
                return;
            }

            if (showMenu) return;

            if (Input.GetKeyDown(KeyCode.Space) && airJumpsUsed < extraJumps)
            {
                airJumpsUsed++;

                // Cancel any falling speed so the extra jump is as strong as a normal one
                Rigidbody rb = playerMovement.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 v = rb.velocity;
                    v.y = 0f;
                    rb.velocity = v;
                }

                jumpMethod.Invoke(playerMovement, null);
            }
        }

        private void ApplyGravity()
        {
            if (gravField == null) return;

            Vector3 current = (Vector3)gravField.GetValue(null);
            if (current == Vector3.zero) return; // game hasn't set it yet

            if (!gotGravityBase)
            {
                baseGravity = current;
                gotGravityBase = true;
            }

            gravField.SetValue(null, baseGravity * gravityMult);
        }

        private void ApplyKeepActivePowerUps()
        {
            if (keepActive.Count == 0) return;
            if (PowerUpManager.Instance == null) return;
            if (Time.unscaledTime < nextPowerTime) return;
            nextPowerTime = Time.unscaledTime + 1f;

            foreach (PowerUpTypes type in keepActive)
            {
                if (!PowerUpManager.Instance.IsPowerUpActive(type))
                    PowerUpManager.Instance.ActivatePowerUp(type);
            }
        }

        private void ApplyEnemyCheats()
        {
            if (Time.unscaledTime < nextEnemyScan) return;
            nextEnemyScan = Time.unscaledTime + 0.25f;

            bool active = oneHitKills || Mathf.Abs(enemyHealthMult - 1f) > 0.01f;
            if (!active && enemyBaseMax.Count == 0) return;

            EnemyHealth[] enemies = UnityEngine.Object.FindObjectsOfType<EnemyHealth>();
            foreach (EnemyHealth e in enemies)
            {
                if (e == null) continue;

                float baseMax;
                if (!enemyBaseMax.TryGetValue(e, out baseMax))
                {
                    if (!active) continue;
                    baseMax = e.maxHealth;
                    enemyBaseMax[e] = baseMax;
                }

                if (oneHitKills)
                {
                    if (e.currentHealth > 0.01f)
                        e.currentHealth = 0.01f;
                    continue;
                }

                float target = baseMax * enemyHealthMult;
                if (e.maxHealth > 0f && Mathf.Abs(e.maxHealth - target) > 0.001f)
                {
                    float ratio = target / e.maxHealth;
                    e.currentHealth *= ratio;
                    e.maxHealth = target;
                }
            }

            // Forget enemies that no longer exist
            List<EnemyHealth> gone = new List<EnemyHealth>();
            foreach (KeyValuePair<EnemyHealth, float> pair in enemyBaseMax)
            {
                if (pair.Key == null)
                    gone.Add(pair.Key);
            }
            foreach (EnemyHealth g in gone)
                enemyBaseMax.Remove(g);

            if (!active)
                enemyBaseMax.Clear();
        }

        private void SetOneHitKills(bool on)
        {
            oneHitKills = on;
            if (!on)
            {
                // Give the enemies their health back
                foreach (KeyValuePair<EnemyHealth, float> pair in enemyBaseMax)
                {
                    if (pair.Key != null)
                        pair.Key.currentHealth = pair.Key.maxHealth;
                }
            }
        }

        private void ResetEverything()
        {
            SetGodMode(false);
            SetOneHitKills(false);
            enemyHealthMult = 1f;
            speedMult = 1f;
            jumpMult = 1f;
            gravityMult = 1f;
            gameSpeed = 1f;
            freezeTime = false;
            doubleJump = false;
            extraJumps = 1;
            keepActive.Clear();
        }

        private void OpenMenu()
        {
            oldLockState = Cursor.lockState;
            oldCursorVisible = Cursor.visible;
            showMenu = true;
        }

        private void CloseMenu()
        {
            showMenu = false;
            Cursor.lockState = oldLockState;
            Cursor.visible = oldCursorVisible;
        }

        // ---------- Menu look ----------

        private Texture2D MakeTex(Color c)
        {
            Texture2D t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }

        private Texture2D MakeBarTex(Color c)
        {
            Texture2D t = new Texture2D(1, 12);
            for (int y = 0; y < 12; y++)
                t.SetPixel(0, y, (y >= 4 && y < 8) ? c : new Color(0f, 0f, 0f, 0f));
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }

        private void SetAllTextColors(GUIStyle s, Color c)
        {
            s.normal.textColor = c;
            s.hover.textColor = c;
            s.active.textColor = c;
            s.onNormal.textColor = c;
            s.onHover.textColor = c;
            s.onActive.textColor = c;
        }

        private void BuildStyles()
        {
            Color bg = new Color(0.07f, 0.07f, 0.09f, 0.96f);
            Color panel = new Color(0.14f, 0.14f, 0.18f, 1f);
            Color panelHover = new Color(0.20f, 0.20f, 0.25f, 1f);
            Color accent = new Color(0.18f, 0.80f, 0.60f, 1f);
            Color text = new Color(0.92f, 0.92f, 0.94f, 1f);
            Color dim = new Color(0.60f, 0.60f, 0.66f, 1f);
            Color dark = new Color(0.05f, 0.05f, 0.07f, 1f);

            Texture2D bgTex = MakeTex(bg);
            Texture2D panelTex = MakeTex(panel);
            Texture2D panelHoverTex = MakeTex(panelHover);
            Texture2D accentTex = MakeTex(accent);
            Texture2D barTex = MakeBarTex(new Color(0.25f, 0.25f, 0.31f, 1f));

            windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.normal.background = bgTex;
            windowStyle.onNormal.background = bgTex;
            windowStyle.border = new RectOffset(0, 0, 0, 0);
            windowStyle.padding = new RectOffset(14, 14, 12, 14);

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 13;
            SetAllTextColors(labelStyle, text);

            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 20;
            SetAllTextColors(titleStyle, accent);

            dimStyle = new GUIStyle(GUI.skin.label);
            dimStyle.fontSize = 11;
            SetAllTextColors(dimStyle, dim);

            valueStyle = new GUIStyle(GUI.skin.label);
            valueStyle.fontSize = 13;
            SetAllTextColors(valueStyle, accent);

            toggleStyle = new GUIStyle(GUI.skin.toggle);
            toggleStyle.fontSize = 13;
            SetAllTextColors(toggleStyle, text);

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 13;
            buttonStyle.border = new RectOffset(0, 0, 0, 0);
            buttonStyle.padding = new RectOffset(8, 8, 6, 6);
            buttonStyle.normal.background = panelTex;
            buttonStyle.hover.background = panelHoverTex;
            buttonStyle.active.background = accentTex;
            buttonStyle.focused.background = panelTex;
            SetAllTextColors(buttonStyle, text);
            buttonStyle.active.textColor = dark;

            tabStyle = new GUIStyle(buttonStyle);

            tabActiveStyle = new GUIStyle(buttonStyle);
            tabActiveStyle.normal.background = accentTex;
            tabActiveStyle.hover.background = accentTex;
            tabActiveStyle.focused.background = accentTex;
            SetAllTextColors(tabActiveStyle, dark);

            sliderStyle = new GUIStyle(GUI.skin.horizontalSlider);
            sliderStyle.normal.background = barTex;
            sliderStyle.hover.background = barTex;
            sliderStyle.active.background = barTex;
            sliderStyle.border = new RectOffset(0, 0, 0, 0);
            sliderStyle.fixedHeight = 12;

            thumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb);
            thumbStyle.normal.background = accentTex;
            thumbStyle.hover.background = accentTex;
            thumbStyle.active.background = accentTex;
            thumbStyle.border = new RectOffset(0, 0, 0, 0);
            thumbStyle.fixedWidth = 12;
            thumbStyle.fixedHeight = 12;

            lineStyle = new GUIStyle();
            lineStyle.normal.background = accentTex;
            lineStyle.fixedHeight = 2;
            lineStyle.margin = new RectOffset(0, 0, 6, 8);

            stylesReady = true;
        }

        public override void OnGUI()
        {
            if (!showMenu) return;
            if (!stylesReady) BuildStyles();

            windowRect = GUILayout.Window(0, windowRect, DrawWindow, "", windowStyle, GUILayout.Width(340));
        }

        private float SliderRow(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(value.ToString("0.0") + "x", valueStyle);
            GUILayout.EndHorizontal();
            value = GUILayout.HorizontalSlider(value, min, max, sliderStyle, thumbStyle);
            GUILayout.Space(6);
            return value;
        }

        private int IntSliderRow(string label, int value, int min, int max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(value.ToString(), valueStyle);
            GUILayout.EndHorizontal();
            float f = GUILayout.HorizontalSlider((float)value, (float)min, (float)max, sliderStyle, thumbStyle);
            GUILayout.Space(6);
            return Mathf.RoundToInt(f);
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("GUNKHACKZ", titleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label("F1 to close", dimStyle);
            GUILayout.EndHorizontal();
            GUILayout.Label("", lineStyle);

            GUILayout.BeginHorizontal();
            for (int i = 0; i < tabNames.Length; i++)
            {
                if (GUILayout.Button(tabNames[i], i == selectedTab ? tabActiveStyle : tabStyle))
                    selectedTab = i;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            if (selectedTab == 0) DrawPlayerTab();
            else if (selectedTab == 1) DrawWorldTab();
            else if (selectedTab == 2) DrawEnemiesTab();
            else DrawPowerUpsTab();

            GUILayout.Space(8);
            GUILayout.Label("", lineStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset everything", buttonStyle))
                ResetEverything();
            if (GUILayout.Button("Close menu", buttonStyle))
                CloseMenu();
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0, 0, 10000, 40));
        }

        private void DrawPlayerTab()
        {
            bool newGod = GUILayout.Toggle(godMode, " God mode", toggleStyle);
            if (newGod != godMode)
                SetGodMode(newGod);
            GUILayout.Space(8);

            speedMult = SliderRow("Move speed", speedMult, 1f, 5f);
            jumpMult = SliderRow("Jump power", jumpMult, 1f, 4f);

            doubleJump = GUILayout.Toggle(doubleJump, " Double jump (press Space in the air)", toggleStyle);
            if (doubleJump)
            {
                GUILayout.Space(4);
                extraJumps = IntSliderRow("Extra air jumps", extraJumps, 1, 5);
            }
        }

        private void DrawWorldTab()
        {
            gravityMult = SliderRow("Gravity", gravityMult, 0.1f, 3f);
            gameSpeed = SliderRow("Game speed", gameSpeed, 0.1f, 5f);

            freezeTime = GUILayout.Toggle(freezeTime, " Freeze time", toggleStyle);
            GUILayout.Label("You freeze too while time is frozen.", dimStyle);
        }

        private void DrawEnemiesTab()
        {
            bool newOneHit = GUILayout.Toggle(oneHitKills, " One-hit kills", toggleStyle);
            if (newOneHit != oneHitKills)
                SetOneHitKills(newOneHit);
            GUILayout.Space(8);

            enemyHealthMult = SliderRow("Enemy health", enemyHealthMult, 0.1f, 10f);
            if (oneHitKills)
                GUILayout.Label("Enemy health is ignored while one-hit kills is on.", dimStyle);
        }

        private void DrawPowerUpsTab()
        {
            GUILayout.Label("Click = activate once. Tick = keep it on.", dimStyle);
            GUILayout.Space(4);

            if (PowerUpManager.Instance == null)
            {
                GUILayout.Label("Start a level to use powerups.", labelStyle);
                return;
            }

            powerScroll = GUILayout.BeginScrollView(powerScroll, GUILayout.Height(220));
            foreach (PowerUpTypes type in powerupTypes)
            {
                GUILayout.BeginHorizontal();

                if (GUILayout.Button(type.ToString(), buttonStyle))
                    PowerUpManager.Instance.ActivatePowerUp(type);

                bool wasKept = keepActive.Contains(type);
                bool nowKept = GUILayout.Toggle(wasKept, " keep", toggleStyle, GUILayout.Width(65));
                if (nowKept && !wasKept) keepActive.Add(type);
                if (!nowKept && wasKept) keepActive.Remove(type);

                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }
    }
}