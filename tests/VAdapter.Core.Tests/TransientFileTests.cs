using VAdapter.Core.Media;
using Xunit;

namespace VAdapter.Core.Tests;

public class TransientFileTests
{
    // --- VoiSona Talk の実測パターン ---

    [Fact]
    public void VoiSonaTalk_TempWav_IsTransient()
    {
        Assert.True(TransientFile.IsTransient(
            @"C:\out\Voice\3_さとうささら_さー_temp97751a69.wav"));
    }

    [Fact]
    public void VoiSonaTalk_TempTxt_IsTransient()
    {
        // txt 側は先頭にドットが付く（隠しファイル扱い）。
        Assert.True(TransientFile.IsTransient(
            @"C:\out\Voice\.3_さとうささら_さー_tempb728ffd1.txt"));
    }

    [Fact]
    public void VoiSonaTalk_FinalFiles_AreNotTransient()
    {
        Assert.False(TransientFile.IsTransient(@"C:\out\Voice\3_さとうささら_さー.wav"));
        Assert.False(TransientFile.IsTransient(@"C:\out\Voice\3_さとうささら_さー.txt"));
    }

    // --- 一般的な一時ファイル ---

    [Theory]
    [InlineData(@"C:\out\voice.wav.vadtmp")]   // V-Adapter 自身の後処理
    [InlineData(@"C:\out\voice.tmp")]
    [InlineData(@"C:\out\voice.part")]
    [InlineData(@"C:\out\voice.crdownload")]
    [InlineData(@"C:\out\~$voice.wav")]
    [InlineData(@"C:\out\.hidden.wav")]
    public void KnownTempConventions_AreTransient(string path) =>
        Assert.True(TransientFile.IsTransient(path));

    // --- 通常のファイルを誤判定しないこと ---

    [Theory]
    [InlineData(@"C:\out\20260830_144653_紲星あかり_テストです.wav")]
    [InlineData(@"C:\out\001_つくよみちゃん（れいせい）_つくよみちゃんです....wav")]
    [InlineData(@"C:\out\4-星界-セリフ.wav")]
    [InlineData(@"C:\out\紲星あかり - コピー_もちもち.wav")]
    public void NormalFiles_AreNotTransient(string path) =>
        Assert.False(TransientFile.IsTransient(path));

    [Fact]
    public void TempWord_WithoutHexIdentifier_IsNotTransient()
    {
        // 「temp」を含むだけの普通の名前まで巻き込まない。
        Assert.False(TransientFile.IsTransient(@"C:\out\temp.wav"));
        Assert.False(TransientFile.IsTransient(@"C:\out\1_ささら_tempo.wav"));
        Assert.False(TransientFile.IsTransient(@"C:\out\体temp測定.wav"));
    }

    [Fact]
    public void TempSuffix_MustBeAtEndOfName()
    {
        // 末尾でなければ最終ファイル名の一部とみなす。
        Assert.False(TransientFile.IsTransient(@"C:\out\1_temp97751a69_ささら.wav"));
    }

    [Fact]
    public void Empty_IsNotTransient()
    {
        Assert.False(TransientFile.IsTransient(""));
        Assert.False(TransientFile.IsTransient("   "));
    }
}
