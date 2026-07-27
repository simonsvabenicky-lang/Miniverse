using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Frontline
{
    /// <summary>
    /// Debug harness so Claude can see the game without a human at the keyboard.
    ///
    /// Activated only by the -autopilot command-line flag, so it is inert in any build a
    /// player would ever run. It drives the soldier left and right on a sine wave, snaps
    /// a PNG every ShotInterval seconds, and quits after Duration — turning "run the game"
    /// into "produce a folder of images", which is a thing a file-reading agent can review.
    ///
    ///   Frontline.exe -autopilot -shotdir "C:\path\to\Shots" -duration 14
    /// </summary>
    public class AutoPilot : MonoBehaviour
    {
        // Overridable because juice is transient: a 1.1s pickup flash fell straight through the
        // 2s default and left no evidence it had fired at all. Anything that flashes needs a
        // shutter faster than it.
        float _shotInterval = 2.0f;

        string _shotDir;
        float _duration = 14f;
        float _t;
        float _nextShot;
        int _shotIndex;
        PlayerController _player;

        public static bool Enabled => HasFlag("-autopilot");

        void Start()
        {
            if (!Enabled) { enabled = false; return; }

            _shotDir = GetArg("-shotdir") ?? Path.Combine(Application.dataPath, "..", "Shots");
            string d = GetArg("-duration");
            if (d != null && float.TryParse(d, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                _duration = parsed;

            string si = GetArg("-shotinterval");
            if (si != null && float.TryParse(si, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedSi)
                && parsedSi > 0f)
                _shotInterval = parsedSi;

            Directory.CreateDirectory(_shotDir);
            _player = FindFirstObjectByType<PlayerController>();

            Debug.Log($"[AutoPilot] on. shots -> {_shotDir}, duration {_duration}s");
        }

        void Update()
        {
            _t += Time.deltaTime;

            // Chase the nearest target, falling back to a lane sweep when the field is empty.
            //
            // This used to be sine-only, which made the bot a hopeless shot: it swept at a
            // fixed rate no matter where the enemies were and only landed hits by coincidence.
            // Difficulty read off that bot says nothing about a human, who tracks. Tracking
            // isn't a *good* player -- no leading, no prioritising -- but it's in the right
            // ballpark, and the sweep still exercises movement and the lane clamp when idle.
            if (_player != null)
            {
                // Prefer parking under an approaching gate so a headless run exercises the
                // shoot-through claim; otherwise track the nearest enemy; otherwise sweep.
                // (For long ramp/fps captures where the bot needs to survive past stage 1, run
                // with -nodie instead of changing this back to enemy-only tracking.)
                //
                // Boss beats both: AutoFirer never aims, it just fires straight ahead from
                // wherever the player is standing (see AutoFirer.cs), so "nearest by Z" alone
                // let the boss -- spawned further back at GateSpawnZ -- sit unshot behind closer
                // regular enemies and gates until it breached the line and despawned unkilled,
                // silently no-opping the checkpoint (-nodie hides a breach; only a kill saves
                // it). Once a boss exists, stand in its lane until it's dead.
                Enemy boss = GameManager.Instance.FindBoss();
                WeaponGate gate = GameManager.Instance.FindNearestGate();
                Enemy target = GameManager.Instance.FindNearestEnemyAhead();
                float x = boss != null ? boss.transform.position.x
                        : gate != null ? gate.transform.position.x
                        : target != null ? target.transform.position.x
                        : Mathf.Sin(_t * 0.9f) * Tuning.LaneHalfWidth;
                _player.SetTargetX(Mathf.Clamp(x, -Tuning.LaneHalfWidth, Tuning.LaneHalfWidth));
            }

            if (_t >= _nextShot)
            {
                _nextShot += _shotInterval;
                string path = Path.Combine(_shotDir, $"shot_{_shotIndex:00}_t{_t:F1}s.png");
                ScreenCapture.CaptureScreenshot(path);
                Debug.Log($"[AutoPilot] captured {path}");
                _shotIndex++;
            }

            if (_t >= _duration)
            {
                Debug.Log("[AutoPilot] done, quitting.");
                Application.Quit();
            }
        }

        static bool HasFlag(string flag)
        {
            foreach (string a in Environment.GetCommandLineArgs())
                if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }
    }
}
