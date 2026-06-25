using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using VAdapter.Automation.Native;

namespace VAdapter.Automation.Aviutl;

/// <summary>
/// ごちゃまぜドロップス（gcmzdrops / AviUtl2版 GCMZDrops2）の外部連携APIクライアント。
/// ファイルマッピング "GCMZDrops" + ミューテックス "GCMZDropsMutex" + WM_COPYDATA で
/// AviUtl/AviUtl2 のタイムラインへファイルを投げ込む。
/// </summary>
public sealed class GcmzApi
{
    private const string MutexName = "GCMZDropsMutex";
    private const string MapName = "GCMZDrops";

    // 共有データ GCMZDropsData のフィールドオフセット（バイト）。
    // Window, Width, Height, VideoRate, VideoScale, AudioRate, AudioCh, GCMZAPIVer (各 int32),
    // ProjectPath (wchar[260]=520B), Flags(v2+), AviUtlVer/GCMZVer(v3+)。
    private const int OffsetWindow = 0;
    private const int OffsetWidth = 4;
    private const int OffsetHeight = 8;
    private const int OffsetVideoRate = 12;
    private const int OffsetVideoScale = 16;
    private const int OffsetApiVer = 28;
    private const int OffsetProjectPath = 32;
    private const int ProjectPathChars = 260;

    /// <summary>外部連携API（=AviUtl+ごちゃまぜドロップス）が利用可能か。</summary>
    public bool IsAvailable()
    {
        var mutex = NativeMethods.OpenMutex(NativeMethods.SYNCHRONIZE, false, MutexName);
        if (mutex == IntPtr.Zero)
            return false;
        NativeMethods.CloseHandle(mutex);
        return true;
    }

    /// <summary>共有メモリから現在の状態を読み取る。利用不可なら null。</summary>
    public GcmzInfo? ReadInfo()
    {
        var mapping = NativeMethods.OpenFileMapping(NativeMethods.FILE_MAP_READ, false, MapName);
        if (mapping == IntPtr.Zero)
            return null;

        var view = NativeMethods.MapViewOfFile(mapping, NativeMethods.FILE_MAP_READ, 0, 0, UIntPtr.Zero);
        try
        {
            if (view == IntPtr.Zero)
                return null;

            var hwnd = new IntPtr(Marshal.ReadInt32(view, OffsetWindow));
            var width = Marshal.ReadInt32(view, OffsetWidth);
            var height = Marshal.ReadInt32(view, OffsetHeight);
            var videoRate = Marshal.ReadInt32(view, OffsetVideoRate);
            var videoScale = Marshal.ReadInt32(view, OffsetVideoScale);
            var apiVer = Marshal.ReadInt32(view, OffsetApiVer);
            var projectPath = Marshal.PtrToStringUni(view + OffsetProjectPath, ProjectPathChars)?
                .TrimEnd('\0') ?? string.Empty;

            return new GcmzInfo
            {
                Window = hwnd,
                Width = width,
                Height = height,
                VideoRate = videoRate,
                VideoScale = videoScale,
                ApiVersion = apiVer,
                ProjectPath = projectPath,
            };
        }
        finally
        {
            if (view != IntPtr.Zero)
                NativeMethods.UnmapViewOfFile(view);
            NativeMethods.CloseHandle(mapping);
        }
    }

    /// <summary>
    /// ファイルを AviUtl/AviUtl2 のタイムラインへ投げ込む。
    /// </summary>
    /// <param name="files">投げ込むファイルのフルパス（1つ以上）。</param>
    /// <param name="layer">挿入レイヤー（負値=表示領域基準の相対 / v2は0=アクティブ）。</param>
    /// <param name="frameAdvance">投入後にカーソルを進めるフレーム数（任意）。</param>
    /// <param name="margin">占有時の間隔（AviUtl2のみ・任意）。</param>
    /// <param name="protocolVersion">COPYDATASTRUCT.dwData（AviUtl=1 / AviUtl2 ExEdit2=2）。</param>
    public GcmzDropResult Drop(
        IReadOnlyList<string> files, int layer, int? frameAdvance, int? margin, int protocolVersion)
    {
        if (files.Count == 0)
            return GcmzDropResult.Fail("投げ込むファイルがありません。");

        var mutexHandle = NativeMethods.OpenMutex(NativeMethods.SYNCHRONIZE, false, MutexName);
        if (mutexHandle == IntPtr.Zero)
            return GcmzDropResult.Fail("AviUtl / ごちゃまぜドロップスが見つかりません（起動・プラグイン導入を確認してください）。");

        bool mutexAcquired = false;
        try
        {
            var wait = NativeMethods.WaitForSingleObject(mutexHandle, 5000);
            if (wait != NativeMethods.WAIT_OBJECT_0 && wait != NativeMethods.WAIT_ABANDONED)
                return GcmzDropResult.Fail("ごちゃまぜドロップスがビジー状態です（時間内に応答しませんでした）。");
            mutexAcquired = true;

            var info = ReadInfo();
            if (info is null || info.Window == IntPtr.Zero)
                return GcmzDropResult.Fail("ごちゃまぜドロップスの共有情報を取得できませんでした。");
            if (!info.HasProject)
                return GcmzDropResult.Fail("AviUtl にプロジェクトが読み込まれていません。");

            var json = BuildJson(files, layer, frameAdvance, margin);
            var bytes = Encoding.UTF8.GetBytes(json);

            var ptr = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, ptr, bytes.Length);
                var cds = new NativeMethods.COPYDATASTRUCT
                {
                    dwData = new IntPtr(protocolVersion),
                    cbData = bytes.Length,
                    lpData = ptr,
                };
                NativeMethods.SendMessage(info.Window, NativeMethods.WM_COPYDATA, IntPtr.Zero, ref cds);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            return GcmzDropResult.Ok();
        }
        catch (Exception ex)
        {
            return GcmzDropResult.Fail($"投げ込みに失敗しました: {ex.Message}");
        }
        finally
        {
            if (mutexAcquired)
                NativeMethods.ReleaseMutex(mutexHandle);
            NativeMethods.CloseHandle(mutexHandle);
        }
    }

    private static string BuildJson(IReadOnlyList<string> files, int layer, int? frameAdvance, int? margin)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteNumber("layer", layer);
            if (frameAdvance is { } fa)
                w.WriteNumber("frameAdvance", fa);
            if (margin is { } m)
                w.WriteNumber("margin", m);
            w.WriteStartArray("files");
            foreach (var f in files)
                w.WriteStringValue(f);
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
