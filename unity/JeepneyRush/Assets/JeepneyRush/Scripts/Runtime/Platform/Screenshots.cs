using System;
using System.Collections;
using System.IO;
using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Real in-game screenshots for the Play Store (and for players):
    ///   • HUD camera button (Settings ▸ Screenshot button), or F12 on PC – with the HUD
    ///   • Pause ▸ PHOTO WITHOUT HUD, or F11 on PC – clean shot of the scene
    ///   • Settings ▸ Store frame 9:16 letterboxes the game so every shot is Play-ready
    /// Saved as 24-bit PNG (no alpha, as Play requires):
    ///   Android 10+  → Pictures/Jeepney Rush (shows up in the Gallery / Google Photos)
    ///   Android 8–9  → Android/data/&lt;package&gt;/files/Screenshots
    ///   Editor       → &lt;project&gt;/Screenshots;  Windows/Mac/Linux → Pictures/Jeepney Rush
    /// </summary>
    public sealed class Screenshots : MonoBehaviour
    {
        public static Screenshots Instance { get; private set; }
        public bool Busy { get; private set; }
        /// <summary>Hides / restores every UI element for a clean shot.</summary>
        public Action<bool> HideUi;
        /// <summary>Hides / restores just the camera button (so it isn't in its own photo).</summary>
        public Action<bool> HideButton;
        /// <summary>Called after saving: message for the player.</summary>
        public Action<string> Saved;

        private void Awake()
        {
            Instance = this;
            Services.UI?.HookScreenshots(this);
        }

        private void Update()
        {
            ApplyFrame();
            if (Busy) return;
            if (InputCompat.KeyPressed(InputCompat.Key.Screenshot)) Take(false);
            else if (InputCompat.KeyPressed(InputCompat.Key.ScreenshotClean)) Take(true);
        }

        public static bool FrameOn => Services.Save != null && Services.Save.Data.storeFrame;

        /// <summary>9:16 frame in screen pixels (origin bottom-left), or the whole screen when off.</summary>
        public static StoreFrame.Rect Frame => FrameOn ? StoreFrame.Compute(Screen.width, Screen.height) : new StoreFrame.Rect(0, 0, Screen.width, Screen.height);

        /// <summary>Keeps the main camera inside the frame (cheap: only writes when it changes).</summary>
        private void ApplyFrame()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var f = Frame;
            var want = new Rect((float)f.X / Mathf.Max(1, Screen.width), (float)f.Y / Mathf.Max(1, Screen.height), (float)f.W / Mathf.Max(1, Screen.width), (float)f.H / Mathf.Max(1, Screen.height));
            if (cam.rect != want) cam.rect = want;
        }

        public void Take(bool clean)
        {
            if (!Busy) StartCoroutine(Capture(clean));
        }

        private IEnumerator Capture(bool clean)
        {
            Busy = true;
            HideButton?.Invoke(true);
            if (clean) HideUi?.Invoke(true);
            yield return null;
            yield return new WaitForEndOfFrame();
            Texture2D shot = null, rgb = null;
            string msg;
            try
            {
                shot = ScreenCapture.CaptureScreenshotAsTexture();
                var f = Frame;
                // crop to the frame and drop the alpha channel (Play wants 24-bit PNG)
                rgb = new Texture2D(f.W, f.H, TextureFormat.RGB24, false);
                rgb.SetPixels(shot.GetPixels(Mathf.Clamp(f.X, 0, shot.width - 1), Mathf.Clamp(f.Y, 0, shot.height - 1),
                    Mathf.Min(f.W, shot.width - f.X), Mathf.Min(f.H, shot.height - f.Y)));
                rgb.Apply(false);
                byte[] png = rgb.EncodeToPNG();
                string name = StoreFrame.FileName(DateTime.Now, clean);
                msg = Save(name, png);
                if (!StoreFrame.PlayAccepts(f.W, f.H)) msg += "  (tip: turn on Store frame 9:16 for Play)";
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                msg = "Screenshot failed: " + e.Message;
            }
            finally
            {
                if (shot != null) Destroy(shot);
                if (rgb != null) Destroy(rgb);
            }
            if (clean) HideUi?.Invoke(false);
            HideButton?.Invoke(false);
            Services.Audio?.Play(Logic.Audio.Sfx.Click, 1f, 0.7f);
            Saved?.Invoke(msg);
            Busy = false;
        }

        /// <summary>Writes the PNG and returns a short "saved to …" message.</summary>
        private static string Save(string name, byte[] png)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (SdkInt() >= 29 && SaveToGallery(name, png)) return "Saved to Gallery ▸ Pictures/Jeepney Rush";
            string dir = Path.Combine(Application.persistentDataPath, "Screenshots");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name), png);
            return "Saved to Android/data/" + Application.identifier + "/files/Screenshots";
#else
            string dir = Application.isEditor
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"))
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Jeepney Rush");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name);
            File.WriteAllBytes(path, png);
            Debug.Log("[Jeepney Rush] Screenshot saved: " + path);
            return "Saved: " + path;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static int SdkInt()
        {
            using (var v = new AndroidJavaClass("android.os.Build$VERSION")) return v.GetStatic<int>("SDK_INT");
        }

        /// <summary>MediaStore insert into Pictures/Jeepney Rush (Android 10+, no storage permission needed).</summary>
        private static bool SaveToGallery(string name, byte[] png)
        {
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
                using (var values = new AndroidJavaObject("android.content.ContentValues"))
                using (var media = new AndroidJavaClass("android.provider.MediaStore$Images$Media"))
                using (var collection = media.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"))
                {
                    values.Call("put", "_display_name", name);
                    values.Call("put", "mime_type", "image/png");
                    values.Call("put", "relative_path", "Pictures/Jeepney Rush");
                    using (var item = resolver.Call<AndroidJavaObject>("insert", collection, values))
                    {
                        if (item == null) return false;
                        using (var stream = resolver.Call<AndroidJavaObject>("openOutputStream", item))
                        {
                            var signed = new sbyte[png.Length];
                            Buffer.BlockCopy(png, 0, signed, 0, png.Length);
                            stream.Call("write", signed);
                            stream.Call("flush");
                            stream.Call("close");
                        }
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Jeepney Rush] Gallery save failed, using the app folder: " + e.Message);
                return false;
            }
        }
#endif
    }
}
