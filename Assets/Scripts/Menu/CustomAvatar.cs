using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public static class CustomAvatar
{
    public const string Prefix = "img:";
    const int Size = 128;
    static readonly Dictionary<string, Sprite> cache = new();

    public static bool Is(string avatar) => avatar != null && avatar.StartsWith(Prefix);

    public static Sprite SpriteOf(string avatar)
    {
        if (!Is(avatar)) return null;
        if (cache.TryGetValue(avatar, out var s) && s != null) return s;
        try
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(Convert.FromBase64String(avatar.Substring(Prefix.Length)))) return null;
            var round = Round(tex);
            UnityEngine.Object.Destroy(tex);
            if (cache.Count > 32) cache.Clear();
            return cache[avatar] = Sprite.Create(round, new Rect(0, 0, round.width, round.height), new Vector2(0.5f, 0.5f), 100f);
        }
        catch (Exception e) { Debug.LogWarning("[Avatar] " + e.Message); return null; }
    }

    public static string FromFile(string path, out string error)
    {
        error = null;
        try
        {
            var bytes = System.IO.File.ReadAllBytes(path);
            if (bytes.Length > 20 * 1024 * 1024) { error = "That picture is too big (over 20 MB)."; return null; }
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!src.LoadImage(bytes)) { error = "Couldn't read that file. Use a PNG or JPG."; return null; }
            int side = Mathf.Min(src.width, src.height);
            var rt = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGB32);
            Vector2 scale = new((float)side / src.width, (float)side / src.height);
            Vector2 offset = new((1f - scale.x) / 2f, (1f - scale.y) / 2f);
            Graphics.Blit(src, rt, scale, offset);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var small = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            small.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            small.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            string data = Prefix + Convert.ToBase64String(small.EncodeToJPG(80));
            UnityEngine.Object.Destroy(src);
            UnityEngine.Object.Destroy(small);
            return data;
        }
        catch (Exception e) { error = "Couldn't load that picture: " + e.Message; return null; }
    }

    static Texture2D Round(Texture2D tex)
    {
        var px = tex.GetPixels32();
        int w = tex.width, h = tex.height;
        float r = Mathf.Min(w, h) / 2f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(w / 2f, h / 2f));
                px[y * w + x].a = (byte)(Mathf.Clamp01(r - d) * 255);
            }
        var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        outTex.SetPixels32(px);
        outTex.Apply();
        return outTex;
    }

    public static string PickFile()
    {
#if UNITY_EDITOR
        string p = UnityEditor.EditorUtility.OpenFilePanelWithFilters("Choose your avatar", "", new[] { "Images", "png,jpg,jpeg" });
        return string.IsNullOrEmpty(p) ? null : p;
#elif UNITY_STANDALONE_WIN
        var ofn = new OpenFileName();
        ofn.structSize = Marshal.SizeOf(ofn);
        ofn.filter = "Images (*.png;*.jpg;*.jpeg)\0*.png;*.jpg;*.jpeg\0\0";
        ofn.file = new string('\0', 1024);
        ofn.maxFile = ofn.file.Length;
        ofn.title = "Choose your avatar";
        ofn.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008;
        return GetOpenFileName(ofn) ? ofn.file.TrimEnd('\0') : null;
#else
        return null;
#endif
    }

    public static bool CanPick =>
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        true;
#else
        false;
#endif

#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    class OpenFileName
    {
        public int structSize;
        public IntPtr dlgOwner = IntPtr.Zero;
        public IntPtr instance = IntPtr.Zero;
        public string filter;
        public string customFilter;
        public int maxCustFilter;
        public int filterIndex;
        public string file;
        public int maxFile;
        public string fileTitle;
        public int maxFileTitle;
        public string initialDir;
        public string title;
        public int flags;
        public short fileOffset;
        public short fileExtension;
        public string defExt;
        public IntPtr custData = IntPtr.Zero;
        public IntPtr hook = IntPtr.Zero;
        public string templateName;
        public IntPtr reservedPtr = IntPtr.Zero;
        public int reservedInt;
        public int flagsEx;
    }
#endif
}
