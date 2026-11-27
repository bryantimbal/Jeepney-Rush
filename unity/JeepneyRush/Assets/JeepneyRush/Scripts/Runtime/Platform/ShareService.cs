using System;
using System.IO;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Shares an image + caption through Android's share sheet (Messenger, Facebook, TikTok…).
    /// Android 10+: the image goes into Pictures/Jeepney Rush (MediaStore, no permission needed) and
    /// its content:// address is shared. Older Android: the caption is shared as text only.
    /// Editor / PC: the image is saved next to the project and the caption is logged.
    /// </summary>
    public static class ShareService
    {
        /// <returns>a short message for the player</returns>
        public static string ShareImage(byte[] png, string fileName, string caption, string chooserTitle = "I-share ang score mo")
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                AndroidJavaObject uri = SdkInt() >= 29 ? InsertImage(activity, fileName, png) : null;
                using var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.SEND");
                if (uri != null)
                {
                    intent.Call<AndroidJavaObject>("setType", "image/png");
                    intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.STREAM", uri);
                    intent.Call<AndroidJavaObject>("addFlags", 1); // FLAG_GRANT_READ_URI_PERMISSION
                }
                else intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", caption);
                using var intentClass = new AndroidJavaClass("android.content.Intent");
                using var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, chooserTitle);
                activity.Call("startActivity", chooser);
                uri?.Dispose();
                return uri != null ? "Saved to Gallery ▸ Pictures/Jeepney Rush" : "";
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Share] " + e.Message);
                return "Hindi ma-share ngayon: " + e.Message;
            }
#else
            string dir = Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"))
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Jeepney Rush");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileName);
            File.WriteAllBytes(path, png);
            Debug.Log($"[Share] score card saved: {path}\n{caption}");
            return "Saved: " + path;
#endif
        }

        /// <summary>Shares plain text (an invite) through the share sheet; on PC it is copied to the clipboard.</summary>
        public static string ShareText(string text, string chooserTitle = "I-invite ang kapitbahay")
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.SEND");
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", text);
                using var intentClass = new AndroidJavaClass("android.content.Intent");
                using var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, chooserTitle);
                activity.Call("startActivity", chooser);
                return "";
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Share] " + e.Message);
                GUIUtility.systemCopyBuffer = text;
                return "Na-copy ang invite.";
            }
#else
            GUIUtility.systemCopyBuffer = text;
            Debug.Log("[Share] " + text);
            return "Na-copy ang invite.";
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static int SdkInt()
        {
            using var v = new AndroidJavaClass("android.os.Build$VERSION");
            return v.GetStatic<int>("SDK_INT");
        }

        /// <summary>MediaStore insert into Pictures/Jeepney Rush; returns the content:// Uri (caller disposes) or null.</summary>
        private static AndroidJavaObject InsertImage(AndroidJavaObject activity, string name, byte[] png)
        {
            try
            {
                using var resolver = activity.Call<AndroidJavaObject>("getContentResolver");
                using var values = new AndroidJavaObject("android.content.ContentValues");
                using var media = new AndroidJavaClass("android.provider.MediaStore$Images$Media");
                using var collection = media.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI");
                values.Call("put", "_display_name", name);
                values.Call("put", "mime_type", "image/png");
                values.Call("put", "relative_path", "Pictures/Jeepney Rush");
                var item = resolver.Call<AndroidJavaObject>("insert", collection, values);
                if (item == null) return null;
                using (var stream = resolver.Call<AndroidJavaObject>("openOutputStream", item))
                {
                    var signed = new sbyte[png.Length];
                    Buffer.BlockCopy(png, 0, signed, 0, png.Length);
                    stream.Call("write", signed);
                    stream.Call("flush");
                    stream.Call("close");
                }
                return item;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Share] gallery save failed: " + e.Message);
                return null;
            }
        }
#endif
    }
}
